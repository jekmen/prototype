using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceAI.Events
{
	public static class EventsBus
	{
	    public class CHandler
	    {
	        private Handler<IEvent> listener;

	        public CHandler(Handler<IEvent> listener)
	        {
	            this.listener = listener;
	        }

	        public Handler<IEvent> Listener
	        {
	            get
	            {
	                return listener;
	            }
	        }
	    }

	    #region Delegates

	    public delegate void Handler<T>(T e) where T : IEvent;

	    #endregion

	    private static readonly Dictionary<Type, List<KeyValuePair<object, CHandler>>> Listeners =
	        new Dictionary<Type, List<KeyValuePair<object, CHandler>>>();

	    private static readonly List<IDeferredAction> CurrentDeferredActions = new List<IDeferredAction>();

	    private static bool _isLocked;

	    private static void Lock(bool value)
	    {
	        _isLocked = value;
	    }

	    private static void AddDeferredAction(IDeferredAction action)
	    {
	        CurrentDeferredActions.Add(action);
	    }


	    private static List<KeyValuePair<object, CHandler>> GetHandlersByType(Type type)
	    {
	        List<KeyValuePair<object, CHandler>> handlers;

	        Listeners.TryGetValue(type, out handlers);

	        return handlers;
	    }

	    public static void AddEventListener<TEvent>(Handler<TEvent> listener) where TEvent : IEvent
	    {
		    if (_isLocked)
		    {
			    AddDeferredAction(new AddDeferredAction(() => { AddEventListenerInternal(listener); }));
			    return;
		    }
		    
		    Lock(true);

		    AddEventListenerInternal(listener);

		    ProcessDeferredActions();

		    Lock(false);
	    }

	    private static void AddEventListenerInternal<TEvent>(Handler<TEvent> listener) where TEvent : IEvent
	    {
		    Type eventType = typeof(TEvent);

		    if (!Listeners.ContainsKey(eventType))
		    {
			    Listeners.Add(eventType, new List<KeyValuePair<object, CHandler>>());
		    }

		    List<KeyValuePair<object, CHandler>> handlers = GetHandlersByType(eventType);

		    if (handlers != null)
		    {
			    Handler<IEvent> listenerCasted666 = (x) => listener.Invoke((TEvent)x);
			    CHandler handler = new CHandler(listenerCasted666);
			    var handlerPair = new KeyValuePair<object, CHandler>(listener, handler);

			    handlers.Add(handlerPair);
		    }
	    }

	    public static void RemoveEventListener<TEvent>(Handler<TEvent> listener) where TEvent : IEvent
	    {
	        if (_isLocked)
	        {
	            AddDeferredAction(new RemoveDeferredAction(() => { RemoveEvenListenerInternal(listener); }));
	            return;
	        }

	        Lock(true);

	        RemoveEvenListenerInternal(listener);

	        ProcessDeferredActions();

	        Lock(false);
	    }

	    private static void ProcessDeferredActions()
	    {
	        for (int i = 0; i < CurrentDeferredActions.Count; i++)
	        {
	            switch (CurrentDeferredActions[i].ActionType())
	            {
	                case DeferredActions.Remove:
	                    (CurrentDeferredActions[i] as RemoveDeferredAction).RemoveHandlerAction();
	                    break;
	                case DeferredActions.Publish:
	                    PublishInternal((CurrentDeferredActions[i] as PublishDeferredAction).Event);
	                    break;
	                case DeferredActions.Add:
		                (CurrentDeferredActions[i] as AddDeferredAction).AddHandlerAction();
		                break;
	            }
	        }

	        CurrentDeferredActions.Clear();
	    }

	    public static void Publish<TEvent>(TEvent _event) where TEvent : IEvent
	    {
	        if (_isLocked)
	        {
	            AddDeferredAction(new PublishDeferredAction(_event));
	            return;
	        }

	        Lock(true);

	        PublishInternal(_event);

	        ProcessDeferredActions();

	        Lock(false);
	    }


	    public static void Clear()
	    {
	        Listeners.Clear();
	    }


	    private static void RemoveEvenListenerInternal<TEvent>(Handler<TEvent> listener) where TEvent : IEvent
	    {
	        foreach (var listeners in Listeners.Values)
	        {
	            listeners.RemoveAll(keyValuePair => listener.Equals(keyValuePair.Key));
	        }
	    }

	    private static void PublishInternal<TEvent>(TEvent _event) where TEvent : IEvent
	    {
	        List<KeyValuePair<object, CHandler>> handlers = GetHandlersByType(_event.GetType());

	        if (handlers == null) return;

	        for (int i = 0; i < handlers.Count; i++)
	        {
	            try
	            {
	                handlers[i].Value.Listener(_event);
	            }
	            catch (Exception ex)
	            {
	                Debug.LogException(ex);
	            }
	        }
	    }
	}
}