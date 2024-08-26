using Community.VisualStudio.Toolkit;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using MSXML;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.VisualStudio.Shell;
using System;

namespace AddPropertyToBinding
{
    [Command(PackageIds.MyCommand)]
    internal sealed class MyCommand : BaseCommand<MyCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            string nameOfProperty = await GetBindingPropertyName();
            string xamlFilePath = await GetActiveFileName();
            bool isActivatedFromXamlFile = IsActivatedFromXamlFile(xamlFilePath);
            if (isActivatedFromXamlFile) 
            {
                string dataContextPath = string.Empty;
                string dataContext = await GetDataContextFromCodeBehindAsync();
                if (dataContext != null) // DataContext = ViewModel
                {
                    string activeProjectName = await GetActiveProjectNameAsync();
                    dataContextPath = CreateProjectPath(xamlFilePath, activeProjectName);
                }
                else // DataContext = this
                {
                    dataContextPath = xamlFilePath + ".cs";
                }
                string dataContextFile = File.ReadAllText(dataContextPath);
                bool hasPropertyRegion = dataContext.Contains("#region Properties");
                bool hasFieldRegion = dataContext.Contains("#region Fields");
                if (!hasFieldRegion) // create fields region
                {

                }
                if (!hasPropertyRegion) // create properties region
                {
                    
                }
                // wrie property after region
            }
        }

        private string CreateProjectPath(string filePath, string projectName) 
        {
            filePath = filePath.Replace("\\", "/");
            string[] pathTable = filePath.Split('/');
            string path = pathTable[0];
            for (int i = 1; i < pathTable.Length; i++) 
            {
                path += "/" + pathTable[i];
                if (pathTable[i] == projectName) break;
            }
            return path;
        }

        private async Task<string> GetActiveProjectNameAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Get the active project
            var project = await VS.Solutions.GetActiveProjectAsync();
            return project?.Name;
        }

        private async Task<string> GetDataContextFromCodeBehindAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Get the active document path
            var docView = await VS.Documents.GetActiveDocumentViewAsync();
            if (docView == null)
            {
                return null;
            }

            // Get the corresponding .xaml.cs file path
            var codeBehindFilePath = docView.FilePath + ".cs";
            if (!File.Exists(codeBehindFilePath))
            {
                // If the file doesn't end with .xaml, get the corresponding .xaml.cs file
                codeBehindFilePath = Path.ChangeExtension(docView.FilePath, ".xaml.cs");
            }

            if (!File.Exists(codeBehindFilePath))
            {
                return null;
            }

            // Read the .xaml.cs file content
            var codeBehindContent = File.ReadAllText(codeBehindFilePath);

            // Search for DataContext assignment using a regex
            var dataContextRegex = new Regex(@"this\.DataContext\s*=\s*new\s+(\w+)\s*\(\s*\)\s*;", RegexOptions.Compiled);
            var match = dataContextRegex.Match(codeBehindContent);

            if (match.Success)
            {
                // Return the type name of the ViewModel assigned to DataContext
                return match.Groups[1].Value;
            }

            return null;
        }

        private bool IsActivatedFromXamlFile(string xamlFileName)
        {
            if (xamlFileName.Contains(".xaml"))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private async Task<string> GetBindingPropertyName()
        {
            // Get the active document view
            var docView = await VS.Documents.GetActiveDocumentViewAsync();
            if (docView == null)
            {
                return null; // No document is active
            }

            // Get the text view from the document view
            var textView = docView.TextView;
            if (textView == null)
            {
                return null;
            }

            // Get the caret position
            SnapshotPoint? caretPoint = textView.Caret?.Position.BufferPosition;
            if (!caretPoint.HasValue)
            {
                return null; // No caret found
            }

            // Get the current snapshot (the current state of the document's text)
            var snapshot = caretPoint.Value.Snapshot;

            // Get the text at the caret position
            var caretPosition = caretPoint.Value;
            var line = snapshot.GetLineFromPosition(caretPosition);
            var text = line.GetText();
            var positionInLine = caretPosition.Position - line.Start.Position;

            // Find the start and end of the word
            int start = positionInLine;
            int end = positionInLine;

            // Move start position backward to the start of the word
            while (start > 0 && !char.IsWhiteSpace(text[start - 1]))
            {
                start--;
            }

            // Move end position forward to the end of the word
            while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != '}')
            {
                end++;
            }

            // Get the word text
            var word = text.Substring(start, end - start);

            return word;
        }

        private async Task<string> GetActiveFileName()
        {
            // Ensure we're running on the UI thread
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Get the active document view
            var docView = await VS.Documents.GetActiveDocumentViewAsync();
            if (docView == null)
            {
                return null; // No document is active
            }

            // Get the file name of the active document
            string fileName = docView.FilePath;

            return fileName;
        }
    }
}
