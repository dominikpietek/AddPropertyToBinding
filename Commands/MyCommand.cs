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
using System.Collections.Generic;
using AddPropertyToBinding.Windows;
using System.Windows.Markup;

namespace AddPropertyToBinding
{
    [Command(PackageIds.MyCommand)]
    internal sealed class MyCommand : BaseCommand<MyCommand>
    {
        private string _propertyType = string.Empty;

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
                    string projPath = CreateProjectPath(xamlFilePath, activeProjectName);
                    string[] files = Directory.GetFiles(projPath, dataContext + ".cs", SearchOption.AllDirectories);
                    dataContextPath = files[0];
                }
                else // DataContext = this
                {
                    dataContextPath = xamlFilePath + ".cs";
                }
                string[] oldDataContextLines = File.ReadAllLines(dataContextPath);

                //add regex
                bool hasPropertyRegion = oldDataContextLines.Contains("#region Properties");
                bool hasFieldRegion = oldDataContextLines.Contains("#region Fields");
                
                List<string> newDataContextLines = new List<string>();

                dataContextPath = dataContextPath.Replace("\\", "/");
                string[] pathTable = dataContextPath.Split('/');
                string fileName = pathTable[pathTable.Length - 1];


                // Get property type
                using (GetTypeWindow window = new GetTypeWindow(SetPropertyType))
                {
                    window.ShowDialog();
                }

                string newLineSign = "\n\r\t\t";
                bool skipNextLine = false;

                for (int i = 0; i < oldDataContextLines.Length; i++)
                {
                    string line = oldDataContextLines[i];

                    if (skipNextLine)
                    {
                        skipNextLine = false;
                        continue;
                    }

                    newDataContextLines.Add(line);

                    if (!hasFieldRegion && line.Contains(dataContext)) // create fields region
                    {
                        newDataContextLines.Add($$"""{{{newLineSign}}#region Fields{{newLineSign}}""" +
                            $"private {_propertyType} _{nameOfProperty};{newLineSign}" +
                            $"#endregion{newLineSign}");
                        hasFieldRegion = true;
                        skipNextLine = true;
                    }
                    if (!hasPropertyRegion && line.Contains(dataContext)) // create properties region
                    {
                        newDataContextLines.Add($$"""#region Properties{{newLineSign}}public {{_propertyType}} {{nameOfProperty}}{{newLineSign}}{{{newLineSign}}get => _{{nameOfProperty}};{{newLineSign}}set{{newLineSign}}{{{newLineSign}}_{{nameOfProperty}} = value;{{newLineSign}}OnPropertyChanged();{{newLineSign}}}{{newLineSign}}}{{newLineSign}}#endregion{{newLineSign}}""");
                        hasPropertyRegion = true;
                    }

                    if (line.Contains("#region Properties"))
                    {
                        newDataContextLines.Add($$"""{{newLineSign}}public {{_propertyType}} {{nameOfProperty}}{{newLineSign}}{{{newLineSign}}get => _{{nameOfProperty}};{{newLineSign}}set{{newLineSign}}{{{newLineSign}}_{{nameOfProperty}} = value;{{newLineSign}}OnPropertyChanged();{{newLineSign}}}{{newLineSign}}}{{newLineSign}}""");
                    }

                    if (line.Contains("#region Fields"))
                    {
                        newDataContextLines.Add($"{newLineSign}private {_propertyType} _{nameOfProperty};{newLineSign}");
                    }
                }

                File.WriteAllText(dataContextPath, string.Join("\n", newDataContextLines));
            }
        }

        private void SetPropertyType(string typeString)
        {
            _propertyType = typeString;
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
