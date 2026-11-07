using System.Windows.Input;
using VectorAtc.Core.Ground;

namespace VectorAtc.ViewModels;

/// <summary>Строка списка наземных услуг: название, состояние, прогресс и кнопка заказа.</summary>
public sealed class GroundServiceItem : ObservableObject
{
    private string _name = "";
    private string _stateText = "";
    private double _progressPercent;
    private GroundServiceState _state;
    private readonly RelayCommand _request;

    public GroundServiceItem(GroundService service, Action<GroundService> request)
    {
        Service = service;
        _request = new RelayCommand(_ => request(service), _ => _state != GroundServiceState.InProgress);
    }

    public GroundService Service { get; }

    public string Name { get => _name; private set => SetProperty(ref _name, value); }

    public string StateText { get => _stateText; private set => SetProperty(ref _stateText, value); }

    public double ProgressPercent { get => _progressPercent; private set => SetProperty(ref _progressPercent, value); }

    public ICommand RequestCommand => _request;

    public void Update(GroundServiceStatus status, string name, string stateText)
    {
        _state = status.State;
        Name = name;
        StateText = stateText;
        ProgressPercent = Math.Round(status.Progress * 100);
        _request.NotifyCanExecuteChanged();
    }
}
