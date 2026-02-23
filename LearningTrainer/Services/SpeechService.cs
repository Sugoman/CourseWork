using System.Diagnostics;
using System.IO;
using System.Media;

namespace LearningTrainer.Services
{
    /// <summary>
    /// Локальный TTS на базе Piper — нейро-движок ONNX, работает полностью офлайн.
    /// Воспроизведение через System.Media.SoundPlayer.
    /// Запустите setup_piper.ps1 для загрузки движка и голосовых моделей.
    /// </summary>
    public class SpeechService : IDisposable
    {
        private readonly string _piperDir;
        private readonly string _piperExe;
        private CancellationTokenSource? _cts;
        private Process? _currentProcess;
        private SoundPlayer? _soundPlayer;
        private string? _tempFile;
        private readonly object _playLock = new();
        private bool _disposed;

        public SpeechService()
        {
            _piperDir = ResolvePiperDir();
            _piperExe = Path.Combine(_piperDir, "piper.exe");
        }

        /// <summary>
        /// Громкость TTS 0–100 (100 = макс).
        /// </summary>
        public int Volume { get; set; } = 100;

        public bool IsConfigured => File.Exists(_piperExe);
        public bool IsAzureConfigured => IsConfigured;

        public void Speak(string text, string language = "English")
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            if (!IsConfigured)
            {
                Debug.WriteLine("[TTS] Piper не установлен. Запустите setup_piper.ps1");
                return;
            }

            // 1. Stop previous
            lock (_playLock) { StopSound(); }
            _cts?.Cancel();
            _cts?.Dispose();

            // 2. Fresh token
            _cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var token = _cts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    var modelPath = FindModel(language);
                    if (modelPath == null)
                    {
                        Debug.WriteLine($"[TTS] Нет голосовой модели для языка: {language}");
                        return;
                    }

                    var tempDir = Path.Combine(Path.GetTempPath(), "LearningTrainerTTS");
                    Directory.CreateDirectory(tempDir);
                    var wavPath = Path.Combine(tempDir, $"tts_{Guid.NewGuid():N}.wav");
                    var ok = await RunPiperAsync(text, modelPath, wavPath, token);
                    if (!ok || token.IsCancellationRequested) return;

                    if (!File.Exists(wavPath) || new FileInfo(wavPath).Length == 0)
                    {
                        Debug.WriteLine("[TTS] Piper не создал WAV файл");
                        return;
                    }

                    // Apply volume scaling to PCM samples
                    if (Volume < 100)
                        ApplyVolume(wavPath, Volume);

                    // Prepend 250 ms of silence to wake up Bluetooth codecs
                    PrependSilence(wavPath, 250);

                    lock (_playLock)
                    {
                        if (token.IsCancellationRequested) return;
                        StopSound();
                        _tempFile = wavPath;

                        _soundPlayer = new SoundPlayer(wavPath);
                        _soundPlayer.Load();  // целиком в память
                        _soundPlayer.Play();  // воспроизведение из памяти
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TTS] {ex.GetType().Name}: {ex.Message}");
                }
            }, token);
        }

        // ── Piper process ────────────────────────────────────────────

        private async Task<bool> RunPiperAsync(
            string text, string modelPath, string outputPath, CancellationToken ct)
        {
            // Replace newlines — piper treats each line as separate utterance
            var cleanText = text.Replace("\r\n", " ").Replace("\n", " ").Trim();
            if (!cleanText.EndsWith("."))
            {
                cleanText = $" {cleanText}. ";
            }

            var psi = new ProcessStartInfo
            {
                FileName = _piperExe,
                Arguments = $"--model \"{modelPath}\" --output_file \"{outputPath}\"",
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _piperDir
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            _currentProcess = process;
            try
            {
                await process.StandardInput.WriteLineAsync(cleanText);
                process.StandardInput.Close();
                await process.WaitForExitAsync(ct);
                return process.ExitCode == 0;
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                throw;
            }
            finally
            {
                _currentProcess = null;
            }
        }

        // ── Model lookup ─────────────────────────────────────────────

        private string? FindModel(string language)
        {
            var voicesDir = Path.Combine(_piperDir, "voices");
            if (!Directory.Exists(voicesDir)) return null;

            var prefix = MapLanguageToModelPrefix(language);

            return Directory.GetFiles(voicesDir, "*.onnx")
                .Where(f => !f.EndsWith(".onnx.json", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(f => Path.GetFileName(f)
                    .StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        private static string MapLanguageToModelPrefix(string language)
        {
            return language.ToLower() switch
            {
                "english" or "en" => "en_US",
                "russian" or "ru" => "ru_RU",
                "german" or "de"  => "de_DE",
                "french" or "fr"  => "fr_FR",
                "spanish" or "es" => "es_ES",
                "italian" or "it" => "it_IT",
                "portuguese" or "pt" => "pt_BR",
                "chinese" or "zh" => "zh_CN",
                "japanese" or "ja" => "ja_JP",
                "korean" or "ko"  => "ko_KR",
                _ => "en_US"
            };
        }

        // ── Piper directory resolution ───────────────────────────────

        private static string ResolvePiperDir()
        {
            // 1. Next to the executable
            var appDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "piper");
            if (File.Exists(Path.Combine(appDir, "piper.exe")))
                return appDir;

            // 2. %LOCALAPPDATA%/LearningTrainer/piper
            var localDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LearningTrainer", "piper");
            if (File.Exists(Path.Combine(localDir, "piper.exe")))
                return localDir;

            return localDir; // default target for setup script
        }

        // ── Volume ───────────────────────────────────────────────

        private static void ApplyVolume(string wavPath, int volumePercent)
        {
            var scale = Math.Clamp(volumePercent, 0, 100) / 100.0;
            var bytes = File.ReadAllBytes(wavPath);
            int dataOffset = FindDataOffset(bytes);

            // Piper outputs 16-bit PCM (2 bytes per sample, little-endian)
            for (int i = dataOffset; i + 1 < bytes.Length; i += 2)
            {
                short sample = (short)(bytes[i] | (bytes[i + 1] << 8));
                sample = (short)Math.Clamp(sample * scale, short.MinValue, short.MaxValue);
                bytes[i] = (byte)(sample & 0xFF);
                bytes[i + 1] = (byte)((sample >> 8) & 0xFF);
            }

            File.WriteAllBytes(wavPath, bytes);
        }

        /// <summary>
        /// Prepends silent PCM samples to the WAV file.
        /// This "wakes up" Bluetooth codecs (A2DP/AAC) that sleep during silence
        /// and would otherwise cut the first ~200 ms of audio.
        /// </summary>
        private static void PrependSilence(string wavPath, int milliseconds)
        {
            if (milliseconds <= 0) return;

            var original = File.ReadAllBytes(wavPath);
            int dataOffset = FindDataOffset(original);

            // Read sample rate and bits-per-sample from fmt chunk
            // Standard WAV: sampleRate at offset 24 (4 bytes LE), bitsPerSample at offset 34 (2 bytes LE)
            int sampleRate = BitConverter.ToInt32(original, 24);
            int channels = BitConverter.ToInt16(original, 22);
            int bitsPerSample = BitConverter.ToInt16(original, 34);
            int bytesPerSample = (bitsPerSample / 8) * channels;

            int silenceSamples = sampleRate * milliseconds / 1000;
            int silenceBytes = silenceSamples * bytesPerSample;

            int oldDataSize = original.Length - dataOffset;
            int newDataSize = oldDataSize + silenceBytes;

            var result = new byte[dataOffset + newDataSize];

            // Copy header
            Array.Copy(original, 0, result, 0, dataOffset);

            // Silence = zero bytes (already default in new array)
            // Copy original PCM after silence
            Array.Copy(original, dataOffset, result, dataOffset + silenceBytes, oldDataSize);

            // Patch RIFF chunk size (offset 4) = fileSize - 8
            int riffSize = result.Length - 8;
            result[4] = (byte)(riffSize & 0xFF);
            result[5] = (byte)((riffSize >> 8) & 0xFF);
            result[6] = (byte)((riffSize >> 16) & 0xFF);
            result[7] = (byte)((riffSize >> 24) & 0xFF);

            // Patch data chunk size (4 bytes before dataOffset)
            int dataSizeOffset = dataOffset - 4;
            result[dataSizeOffset]     = (byte)(newDataSize & 0xFF);
            result[dataSizeOffset + 1] = (byte)((newDataSize >> 8) & 0xFF);
            result[dataSizeOffset + 2] = (byte)((newDataSize >> 16) & 0xFF);
            result[dataSizeOffset + 3] = (byte)((newDataSize >> 24) & 0xFF);

            File.WriteAllBytes(wavPath, result);
        }

        private static int FindDataOffset(byte[] wav)
        {
            for (int i = 12; i < wav.Length - 8; i++)
            {
                if (wav[i] == 'd' && wav[i + 1] == 'a' &&
                    wav[i + 2] == 't' && wav[i + 3] == 'a')
                {
                    return i + 8; // skip "data" + 4-byte size
                }
            }
            return 44; // fallback: standard header size
        }

        // ── Playback ──────────────────────────────────────────────

        private void StopSound()
        {
            if (_soundPlayer != null)
            {
                try { _soundPlayer.Stop(); } catch { }
                _soundPlayer.Dispose();
                _soundPlayer = null;
            }

            if (_tempFile != null)
            {
                try { File.Delete(_tempFile); } catch { }
                _tempFile = null;
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
            try { _currentProcess?.Kill(entireProcessTree: true); } catch { }
            lock (_playLock) { StopSound(); }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                try { _currentProcess?.Kill(entireProcessTree: true); } catch { }
                lock (_playLock) { StopSound(); }
                _disposed = true;
            }
        }
    }
}
