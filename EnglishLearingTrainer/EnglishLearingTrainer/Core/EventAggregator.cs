using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnglishLearningTrainer.Core
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
        }

        public void Publish<T>(T message) where T : class
        {
            var messageType = message.GetType();
            var subscriberTypes = _subscribers.Keys
                .Where(key => key.IsAssignableFrom(messageType));

            foreach (var subscriberType in subscriberTypes)
            {
                foreach (var subscriber in _subscribers[subscriberType])
                {
                    (subscriber as Delegate)?.DynamicInvoke(message);
                }
            }
        }
        public class CloseTabMessage
        {
            public TabViewModelBase TabToClose { get; set; }
        }
    }
}
