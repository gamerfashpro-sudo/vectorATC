using System.Windows.Input;
using VectorAtc.Core.Controller;

namespace VectorAtc.ViewModels;

/// <summary>Строка журнала для показа. Kind нужен окну для подсветки замечаний.</summary>
public sealed record LogLine(string Time, string Speaker, string Text, LogSource Kind);

/// <summary>Быстрая кнопка: подпись и команда, которая отправляет готовую реплику.</summary>
/// <summary>Подсказка-ответ пилота под лентой. IsPrimary: главный ответ на текущее требование диспетчера (подтверждение).</summary>
public sealed record QuickAction(string Label, ICommand Command, bool IsPrimary = false);
