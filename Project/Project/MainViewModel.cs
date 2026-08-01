using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using System.ComponentModel;

namespace Project
{
    // ---------- ViewModelBase ----------
    public class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // ---------- Main ViewModel ----------
    public class MainViewModel : ViewModelBase
    {
        private readonly DrawingService drawingService;

        // DXF Commands
        public ICommand SaveCommand { get; }
        public ICommand OpenCommand { get; }

        // Tool Commands
        public ICommand MoveCommand { get; }
        public ICommand TrimCommand { get; }
        public ICommand ExtendCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand DeleteAllCommand { get; }

        private string prompt = "Ready";
        public string Prompt
        {
            get => prompt;
            set { prompt = value; OnPropertyChanged(nameof(Prompt)); }
        }

        private string currentTool = "";
        public string CurrentTool
        {
            get => currentTool;
            set { currentTool = value; OnPropertyChanged(nameof(CurrentTool)); }
        }

        public MainViewModel(DrawingService service)
        {
            drawingService = service;

            // DXF Commands
            SaveCommand = new RelayCommand(ExecuteSave);
            OpenCommand = new RelayCommand(ExecuteOpen);

            // Tool Commands
            MoveCommand = new RelayCommand(() => CurrentTool = "Move");
            TrimCommand = new RelayCommand(() => CurrentTool = "Trim");
            ExtendCommand = new RelayCommand(() => CurrentTool = "Extend");
            DeleteCommand = new RelayCommand(() => CurrentTool = "Delete");
            DeleteAllCommand = new RelayCommand(ExecuteDeleteAll);
        }

        private void ExecuteSave()
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "AutoCAD DXF (*.dxf)|*.dxf",
                DefaultExt = "dxf",
                Title = "Save Drawing as DXF"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    drawingService.SaveDXF(saveDialog.FileName);
                    Prompt = "Drawing saved to DXF";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving: {ex.Message}", "Error");
                }
            }
        }

        private void ExecuteOpen()
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "AutoCAD DXF (*.dxf)|*.dxf",
                Title = "Open DXF Drawing"
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    drawingService.OpenDXF(openDialog.FileName);
                    Prompt = "Drawing loaded from DXF";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening: {ex.Message}", "Error");
                }
            }
        }

        private void ExecuteDeleteAll()
        {
            if (MessageBox.Show("Are you sure you want to delete ALL shapes?", "Delete All", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                drawingService.DeleteAll();
                Prompt = "All shapes deleted";
            }
        }
    }
}