using Microsoft.Win32;

namespace LearningTrainer.Services.Dialogs
{
    public class DialogService : IDialogService
    {
        public bool ShowSaveDialog(string defaultFileName, out string filePath)
        {
            return ShowSaveDialog(defaultFileName, out filePath, "JSON Files (*.json)|*.json|All Files (*.*)|*.*");
        }

        public bool ShowSaveDialog(string defaultFileName, out string filePath, string filter)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                FileName = defaultFileName,
                Filter = filter,
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
