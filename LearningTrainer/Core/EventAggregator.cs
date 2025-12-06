namespace LearningTrainer.Core
{
    public class EventAggregator
    {
        private static readonly EventAggregator _instance = new EventAggregator();
        public static EventAggregator Instance => _instance;

        private readonly Dictionary<Type, List<object>> _subscribers = new Dictionary<Type, List<object>>();

        public void Subscribe<T>(Action<T> action) where T : class
        {
            var type = typeof(T);
            if (!_subscribers.ContainsKey(type))
            {
                _subscribers[type] = new List<object>();
            }
            _subscribers[type].Add(action);
            actionTargetMap[action] = action;
        }

        public void Unsubscribe<T>(Action<T> action) where T : class
        {
            var type = typeof(T);
            if (_subscribers.TryGetValue(type, out var subscriberList))
            {
                if (actionTargetMap.TryGetValue(action, out var actualSubscriber))
                {
                    subscriberList.Remove(actualSubscriber);
                    actionTargetMap.Remove(action); 
                    if (subscriberList.Count == 0)
                    {
                        _subscribers.Remove(type);
                    }
                }
            }
        }
        private readonly Dictionary<Delegate, object> actionTargetMap = new Dictionary<Delegate, object>();

        public void Publish<T>(T message) where T : class
        {
            var messageType = message.GetType();

            var relevantKeys = _subscribers.Keys
                .Where(key => key.IsAssignableFrom(messageType))
                .ToList(); 

            foreach (var subscriberType in relevantKeys)
            {
                if (_subscribers.TryGetValue(subscriberType, out var originalSubscriberList))
                {
                    var subscribersCopy = originalSubscriberList.ToList();

                    foreach (var subscriber in subscribersCopy)
                    {
                        if (originalSubscriberList.Contains(subscriber))
                        {
                            (subscriber as Delegate)?.DynamicInvoke(message);
                        }
                    }
                }
            }
        }
        public class CloseTabMessage 
        { 
            public TabViewModelBase TabToClose { get; set; } 

            public CloseTabMessage(TabViewModelBase tabToClose) 
            { 
                TabToClose = tabToClose; 
            } 
        } 
    }
}