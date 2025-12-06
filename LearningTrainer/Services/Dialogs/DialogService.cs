using Microsoft.Win32;

namespace LearningTrainer.Services.Dialogs
{
    public class DialogService : IDialogService
    {
        public bool ShowSaveDialog(string defaultFileName, out string filePath)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                FileName = defaultFileName,
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                Title = "Экспорт словаря"
            };

            if (dialog.ShowDialog() == true)
            {
                filePath = dialog.FileName;
                return true;
            }

            filePath = null;
            return false;
        }

        public bool ShowOpenDialog(out string filePath)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                Title = "Импорт словаря"
            };

            if (dialog.ShowDialog() == true)
            {
                filePath = dialog.FileName;
                return true;
            }

            filePath = null;
            return false;
        }
    }
}
