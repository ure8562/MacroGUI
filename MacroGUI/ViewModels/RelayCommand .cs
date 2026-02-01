using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MacroGUI.ViewModels
{
    public sealed class RelayCommand : ICommand
    {

        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        private readonly Action<object?>? _executeParam;
        private readonly Func<object?, bool>? _canExecuteParam;

        public event EventHandler? CanExecuteChanged;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _executeParam = execute;
            _canExecuteParam = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            if (_executeParam != null)
                return _canExecuteParam == null || _canExecuteParam(parameter);

            return _canExecute == null || _canExecute();
        }

        public void Execute(object? parameter)
        {
            if (_executeParam != null)
            {
                _executeParam(parameter);
                return;
            }

            _execute();
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
