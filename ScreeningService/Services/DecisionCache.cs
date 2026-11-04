using ScreeningService.Models;

namespace ScreeningService.Services;

/// <summary>Remembers the decision per requestId so the same requestId always gets the same answer.</summary>
public class DecisionCache
{
    private readonly Dictionary<string, Decision> _decisions = [];
    private readonly object _gate = new();

    public Decision GetOrAdd(string requestId, Func<Decision> decide)
    {
        lock (_gate)
        {
            if (!_decisions.TryGetValue(requestId, out var decision))
                _decisions[requestId] = decision = decide();
            return decision;
        }
    }
}
