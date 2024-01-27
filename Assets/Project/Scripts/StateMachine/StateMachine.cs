using System;
using System.Collections.Generic;
using UnityEngine.XR;

namespace StartledSeal
{
    public class StateMachine
    {
        private StateNode _current;
        private Dictionary<Type, StateNode> _nodes = new();
        private HashSet<ITransition> _anyTransitions = new(); // transition that not need from, can happen anytime

        public void Update()
        {
            var transition = GetTransition();
            if (transition != null)
                ChangeState(transition.To);
            
            _current.State?.OnUpdate();
        }

        public void FixedUpdate()
        {
            _current.State?.OnFixedUpdate();
        }

        public void SetState(IState state)
        {
            _current = _nodes[state.GetType()];
            _current.State.OnEnter();
        }

        private void ChangeState(IState state)
        {
            if (state == _current.State) return;

            var prevState = _current.State;
            var nextState = _nodes[state.GetType()].State;
            
            prevState.OnExit();
            nextState.OnEnter();

            _current = _nodes[state.GetType()];
        }

        public void AddTransition(IState from, IState to, IPredicate condition)
        {
            GetOrAddNode(from).AddTransition(GetOrAddNode(to).State, condition);
        }

        public void AddAnyTransition(IState to, IPredicate condition)
        {
            _anyTransitions.Add(new Transition(to, condition));
        }

        private StateNode GetOrAddNode(IState state)
        {
            var node = _nodes.GetValueOrDefault(state.GetType());

            if (node == null)
            {
                node = new StateNode(state);
                _nodes.Add(state.GetType(), node);
            }

            return node;
        }

        private ITransition GetTransition()
        {
            foreach (var transition in _anyTransitions)
                if (transition.Condition.Evaluate())
                    return transition;
            
            foreach (var transition in _current.Transitions)
                if (transition.Condition.Evaluate())
                    return transition;
            
            return null;
        }
        
        private class StateNode
        {
            public IState State { get; }
            public HashSet<Transition> Transitions { get; }

            public StateNode(IState state)
            {
                State = state;
                Transitions = new HashSet<Transition>();
            }

            public void AddTransition(IState to, IPredicate condition)
            {
                Transitions.Add(new Transition(to, condition));
            }
        }
    }
}