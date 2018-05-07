﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using moddingSuite.BL;
using moddingSuite.BL.Ndf;
using moddingSuite.Model.Edata;
using moddingSuite.Model.Settings;
using moddingSuite.View.Common;
using moddingSuite.View.DialogProvider;
using moddingSuite.View.Ndfbin;
using moddingSuite.ViewModel.About;
using moddingSuite.ViewModel.Base;
using moddingSuite.ViewModel.Media;
using moddingSuite.ViewModel.Mesh;
using moddingSuite.ViewModel.Ndf;
using moddingSuite.ViewModel.Scenario;
using moddingSuite.ViewModel.Trad;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using System.Threading.Tasks;
using System.Windows.Threading;
using moddingSuite.BL.Ess;
using moddingSuite.BL.TGV;
using moddingSuite.BL.Mesh;

namespace moddingSuite.ViewModel.Edata
{
    public class EdataManagerViewModel : ViewModelBase
    {
        private readonly ObservableCollection<EdataFileViewModel> _openFiles = new ObservableCollection<EdataFileViewModel>();

        private string _statusText;

        public string StatusText
        {
            get { return _statusText; }
            set
            {
                _statusText = value;
                OnPropertyChanged(() => StatusText);
            }
        }

        public EdataManagerViewModel()
        {
            InitializeCommands();

            Settings settings = SettingsManager.Load();

            var failedFiles = new List<FileInfo>();

            foreach (var file in settings.LastOpenedFiles)
            {
                var fileInfo = new FileInfo(file);

                if (fileInfo.Exists)
                    try
                    {
                        AddFile(fileInfo.FullName);
                    }
                    catch (IOException)
                    {
                        failedFiles.Add(fileInfo);
                    }
            }

            if (failedFiles.Count > 0)
                StatusText = $"{failedFiles.Count} files failed to open. Did you start the modding suite while running the game?";

            if (settings.LastOpenedFiles.Count == 0)
                CollectionViewSource.GetDefaultView(OpenFiles).MoveCurrentToFirst();

            Workspace = new WorkspaceViewModel(settings);
            Gamespace = new GameSpaceViewModel(settings);

            OpenFiles.CollectionChanged += OpenFilesCollectionChanged;
        }

        public ICommand ExportNdfCommand { get; set; }
        public ICommand ExportRawCommand { get; set; }
        public ICommand ReplaceRawCommand { get; set; }
        public ICommand ExportTextureCommand { get; set; }
        public ICommand ReplaceTextureCommand { get; set; }
        public ICommand ExportSoundCommand { get; set; }
        public ICommand ReplaceSoundCommand { get; set; }
        public ICommand OpenFileCommand { get; set; }
        public ICommand CloseFileCommand { get; set; }
        public ICommand ChangeExportPathCommand { get; set; }
        public ICommand ChangeWargamePathCommand { get; set; }
        public ICommand ChangePythonPathCommand { get; set; }
        public ICommand EditNdfbinCommand { get; set; }
        public ICommand EditTradFileCommand { get; set; }
        public ICommand EditMeshCommand { get; set; }
        public ICommand EditScenarioCommand { get; set; }
        public ICommand PlayMovieCommand { get; set; }
        public ICommand AboutUsCommand { get; set; }
        public ICommand ReplaceRawFromWorkspaceCommand { get; set; }
        public ICommand ReplaceTextureFromWorkspaceCommand { get; set; }
        public ICommand ReplaceSoundFromWorkspaceCommand { get; set; }
        public ICommand OpenEdataFromWorkspaceCommand { get; set; }
        public ICommand AddNewFileCommand { get; set; }


        public ObservableCollection<EdataFileViewModel> OpenFiles
        {
            get { return _openFiles; }
        }

        public WorkspaceViewModel Workspace
        {
            get; set;
        }

        public GameSpaceViewModel Gamespace
        {
            get; set;
        }

        protected void OpenFilesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Settings set = SettingsManager.Load();
            set.LastOpenedFiles.Clear();
            set.LastOpenedFiles.AddRange(OpenFiles.Select(x => x.LoadedFile).ToList());
            SettingsManager.Save(set);
        }

        public void AddFile(string path)
        {
            var vm = new EdataFileViewModel(this);

            vm.LoadFile(path);

            OpenFiles.Add(vm);

            CollectionViewSource.GetDefaultView(OpenFiles).MoveCurrentTo(vm);
        }

        public void CloseFile(EdataFileViewModel vm)
        {
            if (!OpenFiles.Contains(vm))
                return;

            OpenFiles.Remove(vm);
        }

        protected void InitializeCommands()
        {
            OpenFileCommand = new ActionCommand(OpenFileExecute);
            CloseFileCommand = new ActionCommand(CloseFileExecute);

            ChangeExportPathCommand = new ActionCommand(ChangeExportPathExecute);
            ChangeWargamePathCommand = new ActionCommand(ChangeWargamePathExecute);
            ChangePythonPathCommand = new ActionCommand(ChangePythonPathExecute);

            ExportNdfCommand = new ActionCommand(ExportNdfExecute, () => IsOfType(EdataFileType.Ndfbin));
            ExportRawCommand = new ActionCommand(ExportRawExecute);
            ReplaceRawCommand = new ActionCommand(ReplaceRawExecute);
            ExportTextureCommand = new ActionCommand(ExportTextureExecute, () => IsOfType(EdataFileType.Image));
            ReplaceTextureCommand = new ActionCommand(ReplaceTextureExecute, () => IsOfType(EdataFileType.Image));

            ExportSoundCommand = new ActionCommand(ExportSoundExecute, () => HasEnding(".ess"));
            ReplaceSoundCommand = new ActionCommand(ReplaceSoundExecute, () => HasEnding(".ess"));

            PlayMovieCommand = new ActionCommand(PlayMovieExecute);

            AboutUsCommand = new ActionCommand(AboutUsExecute);

            EditTradFileCommand = new ActionCommand(EditTradFileExecute, () => IsOfType(EdataFileType.Dictionary));
            EditNdfbinCommand = new ActionCommand(EditNdfbinExecute, () => IsOfType(EdataFileType.Ndfbin));
            EditMeshCommand = new ActionCommand(EditMeshExecute, () => IsOfType(EdataFileType.Mesh));
            EditScenarioCommand = new ActionCommand(EditScenarioExecute, () => IsOfType(EdataFileType.Scenario));

            AddNewFileCommand = new ActionCommand(AddNewFileExecute);

            ReplaceRawFromWorkspaceCommand = new ActionCommand(ReplaceRawFromWorkspaceExecute);
            ReplaceTextureFromWorkspaceCommand = new ActionCommand(ReplaceTextureFromWorkspaceExecute, () => IsOfType(EdataFileType.Image));
            ReplaceSoundFromWorkspaceCommand = new ActionCommand(ReplaceSoundFromWorkspaceExecute, () => HasEnding(".ess"));
        }

        private void AddNewFileExecute(object obj)
        {
            if (obj is FileViewModel)
            {
                var file = obj as FileViewModel;

                HandleNewFile(file.Info.FullName);
            }
        }

        private void EditScenarioExecute(object obj)
        {
            var vm = CollectionViewSource.GetDefaultView(OpenFiles).CurrentItem as EdataFileViewModel;
            if (vm == null)
                return;

            var scenario = vm.FilesCollectionView.CurrentItem as EdataContentFile;
            if (scenario == null)
                return;

            var dispatcher = Dispatcher.CurrentDispatcher;

            Action<ViewModelBase, ViewModelBase> open = DialogProvider.ProvideView;
            Action<string> report = msg => StatusText = msg;

            var s = new Task(() =>
            {
                try
                {
                    dispatcher.Invoke(() => IsUIBusy = true);
                    dispatcher.Invoke(report, "Reading scenario...");



                    var detailsVm = new ScenarioEditorViewModel(scenario, vm);

                    dispatcher.Invoke(open, detailsVm, this);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Unhandeled exception in Thread occoured: {0}", ex.ToString());
                }
                finally
                {
                    dispatcher.Invoke(() => IsUIBusy = false);
                    dispatcher.Invoke(report, "Ready");
                }
            });

            s.Start();
        }

        private void EditMeshExecute(object obj)
        {
            var vm = CollectionViewSource.GetDefaultView(OpenFiles).CurrentItem as EdataFileViewModel;
            if (vm == null)
                return;

            var mesh = vm.FilesCollectionView.CurrentItem as EdataContentFile;
            if (mesh == null)
                return;

            var dispatcher = Dispatcher.CurrentDispatcher;

            Action<ViewModelBase, ViewModelBase> open = DialogProvider.ProvideView;
            Action<string> report = msg => StatusText = msg;

            var s = new Task(() =>
            {
                try
                {
                    dispatcher.Invoke(() => IsUIBusy = true);
                    dispatcher.Invoke(report, "Reading Mesh package...");

                    var reader = new MeshReader();
                    var meshfile = reader.Read(vm.EdataManager.GetRawData(mesh));

                    var detailsVm = new MeshEditorViewModel(meshfile);

                    dispatcher.Invoke(open, detailsVm, this);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Unhandeled exception in Thread occoured: {0}", ex.ToString());
                }
                finally
                {
                    dispatcher.Invoke(() => IsUIBusy = false);
                    dispatcher.Invoke(report, "Ready");
                }
            });

            s.Start();
        }

        private void ReplaceRawExecute(object obj)
        {
            Settings settings = SettingsManager.Load();

            var openfDlg = new OpenFileDialog
            {
                //DefaultExt = ".*",
                Multiselect = false,
                Filter = "All files (*.*)|*.*"
            };

            if (File.Exists(settings.LastOpenFolder))
                openfDlg.InitialDirectory = settings.LastOpenFolder;

            if (openfDlg.ShowDialog().Value)
            {
                settings.LastOpenFolder = new FileInfo(openfDlg.FileName).DirectoryName;
                SettingsManager.Save(settings);

                ReplaceRawFile(File.ReadAllBytes(openfDlg.FileName));
            }
        }

        protected void ReplaceTextureExecute(object obj)
        {
            Settings settings = SettingsManager.Load();

            var openfDlg = new OpenFileDialog
            {
                DefaultExt = ".dds",
                Multiselect = false,
                Filter = "DDS files (.dds)|*.dds"
            };

            if (File.Exists(settings.LastOpenFolder))
                openfDlg.InitialDirectory = settings.LastOpenFolder;

            if (openfDlg.ShowDialog().Value)
            {
                settings.LastOpenFolder = new FileInfo(openfDlg.FileName).DirectoryName;
                SettingsManager.Save(settings);

                ReplaceTextureFile(openfDlg.FileName);
            }
        }

        private void ReplaceSoundExecute(object obj)
        {
            Settings settings = SettingsManager.Load();

            var openfDlg = new OpenFileDialog
            {
                DefaultExt = ".wav",
                Multiselect = false,
                Filter = "WAV files (.wav)|*.wav"
            };

            if (File.Exists(settings.LastOpenFolder))
                openfDlg.InitialDirectory = settings.LastOpenFolder;

            if (openfDlg.ShowDialog().Value)
            {
                settings.LastOpenFolder = new FileInfo(openfDlg.FileName).DirectoryName;
                SettingsManager.Save(settings);

                ReplaceSoundFile(openfDlg.FileName);
            }
        }

        private void ReplaceRawFromWorkspaceExecute(object obj)
        {
            var file = obj.ToString();

            if (File.Exists(file))
            {
                ReplaceRawFile(File.ReadAllBytes(file));
            }
        }

        private void ReplaceTextureFromWorkspaceExecute(object obj)
        {
            var file = obj.ToString();

            if (File.Exists(file))
            {
                ReplaceTextureFile(file);
            }
        }

        private void ReplaceSoundFromWorkspaceExecute(object obj)
        {
            var file = obj.ToString();

            if (File.Exists(file))
            {
                ReplaceSoundFile(file);
            }
        }

        private void ReplaceRawFile(byte[] newFileData)
        {
            var vm = CollectionViewSource.GetDefaultView(OpenFiles).CurrentItem as EdataFileViewModel;
            var file = vm?.FilesCollectionView.CurrentItem as EdataContentFile;

            if (file == null)
                return;

            var dispatcher = Dispatcher.CurrentDispatcher;

            Action<string> report = msg => StatusText = msg;

            var s = new Task(() =>
            {
                try
                {
                    dispatcher.Invoke(() => IsUIBusy = true);
                    dispatcher.Invoke(report, $"Replacing {file.Path}...");

                    vm.EdataManager.ReplaceFile(file, newFileData);
                    vm.LoadFile(vm.LoadedFile);

                    dispatcher.Invoke(report, "Ready");
                }
                catch (Exception ex)
                {
                    dispatcher.Invoke(report, $"Replacing failed {ex.Message}");
                    Trace.TraceError("Unhandeled exception in Thread occoured: {0}", ex.ToString());
                }
                finally
                {
                    dispatcher.Invoke(() => IsUIBusy = false);
                }
            });

            s.Start();
        }

        protected void ReplaceTextureFile(string newFilePath)
        {
            var vm = CollectionViewSource.GetDefaultView(OpenFiles).CurrentItem as EdataFileViewModel;
            var destTgvFile = vm?.FilesCollectionView.CurrentItem as EdataContentFile;

            if (destTgvFile == null)
                return;

            var tgvReader = new TgvReader();
            var data = vm.EdataManager.GetRawData(destTgvFile);
            var tgv = tgvReader.Read(data);

            var dispatcher = Dispatcher.CurrentDispatcher;
            Action<string> report = msg => StatusText = msg;

            var s = new Task(() =>
            {
                try
                {
                    dispatcher.Invoke(() => IsUIBusy = true);
                    dispatcher.Invoke(report, $"Replacing {destTgvFile.Path}...");

                    byte[] sourceDds = File.ReadAllBytes(newFilePath);

                    dispatcher.Invoke(report, "Converting DDS to TGV file format...");

                    var ddsReader = new TgvDDSReader();
                    var sourceTgvFile = ddsReader.ReadDDS(sourceDds);
                    byte[] sourceTgvRawData;

                    using (var tgvwriterStream = new MemoryStream())
                    {
                        var tgvWriter = new TgvWriter();
                        tgvWriter.Write(tgvwriterStream, sourceTgvFile, tgv.SourceChecksum, tgv.IsCompressed);
                        sourceTgvRawData = tgvwriterStream.ToArray();
                    }

                    dispatcher.Invoke(report, "Replacing file in edata container...");

                    vm.EdataManager.ReplaceFile(destTgvFile, sourceTgvRawData);

                    vm.LoadFile(vm.LoadedFile);
                    dispatcher.Invoke(report, "Ready");
                }
                catch (Exception ex)
                {
                    dispatcher.Invoke(report, $"Replacing failed {ex.Message}");
                    Trace.TraceError("Unhandeled exception in Thread occoured: {0}", ex.ToString());
                }
                finally
                {
                    dispatcher.Invoke(() => IsUIBusy = false);
                }
            });

            s.Start();
        }

        protected void ReplaceSoundFile(string newFilePath)
        {
            var vm = CollectionViewSource.GetDefaultView(OpenFiles).CurrentItem as EdataFileViewModel;

            if (vm == null)
                return;

            var file = vm.FilesCollectionView.CurrentItem as EdataContentFile;

            if (file == null)
                return;

            var dispatcher = Dispatcher.CurrentDispatcher;
            Action<string> report = msg => StatusText = msg;

            var s = new Task(() =>
            {
                try
                {
                    dispatcher.Invoke(() => IsUIBusy = true);
                    dispatcher.Invoke(report, $"Replacing {file.Path}...");
                    byte[] replacefile = File.ReadAllBytes(newFilePath);

                    EssWriter writer = new EssWriter();

                    try
                    {
                        replacefile = writer.Write(replacefile);
                        vm.EdataManager.ReplaceFile(file, replacefile);
                        vm.LoadFile(vm.LoadedFile);
                        dispatcher.Invoke(report, "Ready");
                    }
                    catch (InvalidDataException ex)
                    {
                        dispatcher.Invoke(report, ex.Message);
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Unhandeled exception in Thread occoured: {0}", ex.ToString());
                }
                finally
                {
                    dispatcher.Invoke(() => IsUIBusy = false);
                }
            });

            s.Start();
        }

        protected void ExportTextureExecute(object obj)
        {
            var vm = CollectionViewSource.GetDefaultView(OpenFiles).CurrentItem as EdataFileViewModel;

            var sourceTgvFile = vm?.FilesCollectionView.CurrentItem as EdataContentFile;

            if (sourceTgvFile == null)
                return;

            var dispatcher = Dispatcher.CurrentDispatcher;
            Action<string> report = msg => StatusText = msg;

            var s = new Task(() =>
            {
                try
                {
                    dispatcher.Invoke(() => IsUIBusy = true);

                    Settings settings = SettingsManager.Load();

                    var f = new FileInfo(sourceTgvFile.Path);
                    var exportPath = Path.Combine(settings.SavePath, f.Name + ".dds");

                    dispatcher.Invoke(report, string.Format("Exporting to {0}...", exportPath));

                    var tgvReader = new TgvReader();
                    var tgv = tgvReader.Read(vm.EdataManager.GetRawData(sourceTgvFile));

                    var writer = new TgvDDSWriter();

                    byte[] content = writer.CreateDDSFile(tgv);

                    using (var fs = new FileStream(Path.Combine(settings.SavePath, f.Name + ".dds"), FileMode.OpenOrCreate))
                    {
                        fs.Write(content, 0, content.Length);
                        fs.Flush();
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Unhandeled exception in Thread occoured: {0}", ex.ToString());
                }
                finally
                {
                    dispatcher.Invoke(report, "Ready");
                    dispatcher.Invoke(() => IsUIBusy = false);
                }
            });

            s.Start();
        }

        private void ExportSoundExecute(object obj)
        {
            var vm = CollectionViewSource.GetDefaultView(OpenFiles).CurrentItem as EdataFileViewModel;

            if (vm == null)
                return;

            var sourceEssFile = vm.FilesCollectionView.CurrentItem as EdataContentFile;

            if (sourceEssFile == null)
                return;

            var dispatcher = Dispatcher.CurrentDispatcher;
            Action<string> report = msg => StatusText = msg;

            var s = new Task(() =>
            {
                try
                {
                    dispatcher.Invoke(() => IsUIBusy = true);

                    Settings settings = SettingsManager.Load();

                    var f = new FileInfo(sourceEssFile.Path);
                    var exportPath = Path.Combine(settings.SavePath, f.Name + ".wav");

                    dispatcher.Invoke(report, string.Format("Exporting to {0}...", exportPath));

                    var tgvReader = new EssReader();
                    var tgv = tgvReader.ReadEss(vm.EdataManager.GetRawData(sourceEssFile));

                    using (var fs = new FileStream(Path.Combine(settings.SavePath, f.Name + ".wav"), FileMode.OpenOrCreate))
                    {
                        fs.Write(tgv, 0, tgv.Length);
                        fs.Flush();
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Unhandeled exception in Thread occoured: {0}", ex.ToString());
                }
                finally
                {
                    dispatcher.Invoke(report, "Ready");
                    dispatcher.Invoke(() => IsUIBusy = false);
                }
            });

            s.Start();
        }

        protected bool IsOfType(EdataFileType type)
        {
            var vm = CollectionViewSource.GetDefaultView(OpenFiles).CurrentItem as EdataFileViewModel;

            var ndf = vm?.FilesCollectionView.CurrentItem as EdataContentFile;

            return ndf?.FileType == type;
        }

        protected bool HasEnding(string ending)
        {
            var vm = CollectionViewSource.GetDefaultView(OpenFiles).CurrentItem as EdataFileViewModel;

            var ndf = vm?.FilesCollectionView.CurrentItem as EdataContentFile;

            return ndf != null && ndf.Name.EndsWith(ending);
        }

        protected void EditTradFileExecute(object obj)
        {
            var vm = CollectionViewSource.GetDefaultView(OpenFiles).CurrentItem as EdataFileViewModel;

            if (vm == null)
                return;

            var ndf = vm.FilesCollectionView.CurrentItem as EdataContentFile;

            if (ndf == null)
                return;

            var tradVm = new TradFileViewModel(ndf, vm);

            DialogProvider.ProvideView(tradVm, this);
        }

        protected void EditNdfbinExecute(object obj)
        {
            var vm = CollectionViewSource.GetDefaultView(OpenFiles).CurrentItem as EdataFileViewModel;

            if (vm == null)
                return;

            var ndf = vm.FilesCollectionView.CurrentItem as EdataContentFile;

            if (ndf == null)
                return;

            var dispatcher = Dispatcher.CurrentDispatcher;

            Action<ViewModelBase, ViewModelBase> open = DialogProvider.ProvideView;
            Action<string> report = msg => StatusText = msg;

            var s = new Task(() =>
                {
                    try
                    {
                        dispatcher.Invoke(() => IsUIBusy = true);
                        dispatcher.Invoke(report, "Decompiling ndf binary...");

                        var detailsVm = new NdfEditorMainViewModel(ndf, vm);
                        dispatcher.Invoke(open, detailsVm, this);
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("Unhandeled exception in Thread occoured: {0}", ex.ToString());
                    }
                    finally
                    {
                        dispatcher.Invoke(() => IsUIBusy = false);
                        dispatcher.Invoke(report, "Ready");
                    }
                });

            s.Start();

        }

        protected void ExportNdfExecute(object obj)
        {
            var vm = CollectionViewSource.GetDefaultView(OpenFiles).CurrentItem as EdataFileViewModel;

            if (vm == null)
                return;

            var ndf = vm.FilesCollectionView.CurrentItem as EdataContentFile;

            if (ndf == null)
                return;

            Settings settings = SettingsManager.Load();

            byte[] content = new NdfbinReader().GetUncompressedNdfbinary(vm.EdataManager.GetRawData(ndf));

            var f = new FileInfo(ndf.Path);

            using (var fs = new FileStream(Path.Combine(settings.SavePath, f.Name), FileMode.OpenOrCreate))
            {
                fs.Write(content, 0, content.Length);
                fs.Flush();
            }
        }

        protected void ExportRawExecute(object obj)
        {
            var vm = CollectionViewSource.GetDefaultView(OpenFiles).CurrentItem as EdataFileViewModel;

            if (vm == null)
                return;

            var ndf = vm.FilesCollectionView.CurrentItem as EdataContentFile;

            if (ndf == null)
                return;

            var dispatcher = Dispatcher.CurrentDispatcher;
            Action<string> report = msg => StatusText = msg;

            var s = new Task(() =>
            {
                try
                {
                    dispatcher.Invoke(() => IsUIBusy = true);

                    Settings settings = SettingsManager.Load();

                    var f = new FileInfo(ndf.Path);

                    string exportFullName = Path.Combine(settings.SavePath, settings.ExportWithFullPath ? ndf.Path : f.Name);
                    var exportDir = Path.GetDirectoryName(exportFullName);

                    if (!Directory.Exists(exportDir))
                        Directory.CreateDirectory(exportDir);

                    dispatcher.Invoke(report, string.Format("Exporting to {0}...", exportFullName));

                    byte[] buffer = vm.EdataManager.GetRawData(ndf);

                    using (var fs = new FileStream(exportFullName, FileMode.OpenOrCreate))
                    {
                        fs.Write(buffer, 0, buffer.Length);
                        fs.Flush();
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Unhandeled exception in Thread occoured: {0}", ex.ToString());
                }
                finally
                {
                    dispatcher.Invoke(report, "Ready");
                    dispatcher.Invoke(() => IsUIBusy = false);
                }
            });

            s.Start();
        }

        protected void ExportAll()
        {
            //foreach (var file in Files)
            //{
            //    var f = new FileInfo(file.Path);

            //    var dirToCreate = Path.Combine("c:\\temp\\", f.DirectoryName);

            //    if (!Directory.Exists(dirToCreate))
            //        Directory.CreateDirectory(dirToCreate);

            //    var buffer = NdfManager.GetRawData(file);
            //    using (var fs = new FileStream(Path.Combine(dirToCreate, f.Name), FileMode.OpenOrCreate))
            //    {
            //        fs.Write(buffer, 0, buffer.Length);
            //        fs.Flush();
            //    }

            //}
        }

        protected void ChangeExportPathExecute(object obj)
        {
            Settings settings = SettingsManager.Load();

            var folderDlg = new FolderBrowserDialog
            {
                SelectedPath = settings.SavePath,
                //RootFolder = Environment.SpecialFolder.MyComputer,
                ShowNewFolderButton = true,
            };

            if (folderDlg.ShowDialog() == DialogResult.OK)
            {
                settings.SavePath = folderDlg.SelectedPath;
                SettingsManager.Save(settings);
            }
        }

        protected void ChangeWargamePathExecute(object obj)
        {
            Settings settings = SettingsManager.Load();

            var folderDlg = new FolderBrowserDialog
            {
                SelectedPath = settings.WargamePath,
                //RootFolder = Environment.SpecialFolder.MyComputer,
                ShowNewFolderButton = true,
            };

            if (folderDlg.ShowDialog() == DialogResult.OK)
            {
                settings.WargamePath = folderDlg.SelectedPath;
                SettingsManager.Save(settings);
            }
        }

        private void ChangePythonPathExecute(object obj)
        {
            Settings settings = SettingsManager.Load();

            var folderDlg = new FolderBrowserDialog
            {
                SelectedPath = settings.PythonPath,
                RootFolder = Environment.SpecialFolder.MyComputer,
                ShowNewFolderButton = true,
            };

            if (folderDlg.ShowDialog() == DialogResult.OK)
            {
                settings.PythonPath = folderDlg.SelectedPath;
                SettingsManager.Save(settings);
            }
        }

        protected void OpenFileExecute(object obj)
        {
            Settings settings = SettingsManager.Load();

            var openfDlg = new OpenFileDialog
            {
                DefaultExt = ".dat",
                Multiselect = true,
                Filter = "Edat (.dat)|*.dat|All Files|*.*"
            };

            if (File.Exists(settings.LastOpenFolder))
                openfDlg.InitialDirectory = settings.LastOpenFolder;


            if (openfDlg.ShowDialog().Value)
            {
                settings.LastOpenFolder = new FileInfo(openfDlg.FileName).DirectoryName;
                SettingsManager.Save(settings);
                foreach (string fileName in openfDlg.FileNames)
                {
                    HandleNewFile(fileName);
                }
            }
        }

        private void HandleNewFile(string fileName)
        {
            EdataFileType type;

            using (var fs = new FileStream(fileName, FileMode.Open))
            {
                var headerBuffer = new byte[12];
                fs.Read(headerBuffer, 0, headerBuffer.Length);

                type = EdataManager.GetFileTypeFromHeaderData(headerBuffer);

                if (type == EdataFileType.Ndfbin)
                {
                    var buffer = new byte[fs.Length];

                    fs.Seek(0, SeekOrigin.Begin);
                    fs.Read(buffer, 0, buffer.Length);

                    var detailsVm = new NdfEditorMainViewModel(buffer);

                    var view = new NdfbinView { DataContext = detailsVm };

                    view.Show();
                }
            }

            if (type == EdataFileType.Package)
                AddFile(fileName);
        }

        protected void CloseFileExecute(object obj)
        {
            CloseFile(CollectionViewSource.GetDefaultView(OpenFiles).CurrentItem as EdataFileViewModel);
        }

        protected void PlayMovieExecute(object obj)
        {
            const string name = "temp.wmv";
            var vm = CollectionViewSource.GetDefaultView(OpenFiles).CurrentItem as EdataFileViewModel;

            if (vm == null)
                return;

            var ndf = vm.FilesCollectionView.CurrentItem as EdataContentFile;

            if (ndf == null)
                return;

            Settings settings = SettingsManager.Load();

            byte[] buffer = vm.EdataManager.GetRawData(ndf);

            //var f = new FileInfo(ndf.Path);

            using (var fs = new FileStream(Path.Combine(settings.SavePath, name), FileMode.OpenOrCreate))
            {
                fs.Write(buffer, 0, buffer.Length);
                fs.Flush();
            }

            var detailsVm = new MoviePlaybackViewModel(Path.Combine(settings.SavePath, name));

            var view = new MoviePlaybackView { DataContext = detailsVm };

            view.Show();
        }

        protected void AboutUsExecute(object obj)
        {
            DialogProvider.ProvideView(new AboutViewModel(), this);
        }
    }
}