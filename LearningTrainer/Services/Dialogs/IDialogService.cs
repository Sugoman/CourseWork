using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LearningTrainer.Services.Dialogs
{
    public interface IDialogService
    {
        bool ShowSaveDialog(string defaultFileName, out string filePath);
        bool ShowSaveDialog(string defaultFileName, out string filePath, string filter);
        bool ShowOpenDialog(out string filePath);
    }
}
