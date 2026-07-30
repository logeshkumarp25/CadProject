using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Project
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private DrawingService drawingService;
        private string prompt = "Ready";
        private string commandText = "";
        private string currentTool = "";

        public string Prompt { get => prompt; set { prompt = value; OnPropertyChanged(); } }
        public string CommandText { get => commandText; set { commandText = value; OnPropertyChanged(); } }
        public string CurrentTool { get => currentTool; set { currentTool = value; OnPropertyChanged(); } }

        public ICommand LineCommand { get; }
        public ICommand CircleCommand { get; }
        public ICommand ArcCommand { get; }
        public ICommand MoveCommand { get; }
        public ICommand TrimCommand { get; }
        public ICommand ExtendCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand OpenCommand { get; }
        public ICommand DeleteAllCommand { get; }

        public MainViewModel(DrawingService drawingService)
        {
            this.drawingService = drawingService;
            LineCommand = new RelayCommand(() => { CurrentTool = "Line"; Prompt = "Specify first point"; CommandText = "LINE"; });
            CircleCommand = new RelayCommand(() => { CurrentTool = "Circle"; Prompt = "Specify center point"; CommandText = "CIRCLE"; });
            ArcCommand = new RelayCommand(() => { CurrentTool = "Arc"; Prompt = "Specify start point"; CommandText = "ARC"; });
            MoveCommand = new RelayCommand(() => { CurrentTool = "Move"; Prompt = "Select object"; CommandText = "MOVE"; });
            TrimCommand = new RelayCommand(() => { CurrentTool = "Trim"; Prompt = "Select cutting edge"; CommandText = "TRIM"; });
            ExtendCommand = new RelayCommand(() => { CurrentTool = "Extend"; Prompt = "Select boundary edge"; CommandText = "EXTEND"; });
            DeleteCommand = new RelayCommand(() => { CurrentTool = "Delete"; Prompt = "Select object"; CommandText = "DELETE"; });
            SaveCommand = new RelayCommand(() => { drawingService.Save(); Prompt = "Drawing Saved"; });
            OpenCommand = new RelayCommand(() => { drawingService.Open(); Prompt = "Drawing Opened"; });
            DeleteAllCommand = new RelayCommand(() => { drawingService.DeleteAll(); Prompt = "All objects deleted"; CurrentTool = ""; });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}