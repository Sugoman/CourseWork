using System.Threading.Channels;

namespace LearningAPI.Services;

/// <summary>
/// Запрос на фоновое получение транскрипции.
/// </summary>
public sealed record TranscriptionRequest(int WordId, string OriginalWord);

/// <summary>
/// Bounded Channel для передачи задач «получить транскрипцию»
/// из контроллера в фоновый воркер.
/// </summary>
public sealed class TranscriptionChannel
{
    private readonly Channel<TranscriptionRequest> _channel;

    public TranscriptionChannel()
    {
        var options = new BoundedChannelOptions(capacity: 500)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<TranscriptionRequest>(options);
    }

    public ChannelWriter<TranscriptionRequest> Writer => _channel.Writer;
    public ChannelReader<TranscriptionRequest> Reader => _channel.Reader;
}
