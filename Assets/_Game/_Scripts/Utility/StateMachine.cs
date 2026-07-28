using System;
using System.Collections.Generic;

public class StateMachine
{
    private class Transition
    {
        public IState To;
        public Func<bool> Condition;
    }

    private IState currentState;
    private readonly Dictionary<Type, List<Transition>> transitions = new();
    private List<Transition> currentTransitions = new();
    private readonly List<Transition> anyTransitions = new();
    private static readonly List<Transition> Empty = new();

    public string CurrentStateName => currentState.GetType().Name ?? "none";

    public void Tick()
    {
        Transition transition = GetTransition();
        if (transition != null)
            SetState(transition.To);
        currentState?.Tick();
    }

    public void SetState(IState state)
    {
        if (state == currentState) return;
        currentState?.Exit();
        currentState = state;
        if (!transitions.TryGetValue(currentState.GetType(), out currentTransitions))
            currentTransitions = Empty;
        currentState.Enter();
    }

    public void AddTransition(IState from, IState to, Func<bool> condition)
    {
        if (!transitions.TryGetValue(from.GetType(), out List<Transition> list))
        {
            list = new List<Transition>();
            transitions[from.GetType()] = list;
        }
        list.Add(new Transition { To = to, Condition = condition} );
    }

    public void AddAnyTransition(IState to, Func<bool> condition)
    => anyTransitions.Add(new Transition {To = to, Condition = condition });

    private Transition GetTransition()
    {
        foreach (Transition t in anyTransitions)
            if (t.Condition()) return t;
        foreach (Transition t in currentTransitions)
            if (t.Condition()) return t;
        return null;
    }
}
