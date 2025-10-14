using System;
using System.Collections.Generic;
using System.Linq;
// ... (другие using'и)

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
            // Запомним подписчика (сам делегат) для отписки
            actionTargetMap[action] = action;
        }

        // --- ВОТ ЭТОТ МЕТОД ---
        public void Unsubscribe<T>(Action<T> action) where T : class
        {
            var type = typeof(T);
            if (_subscribers.TryGetValue(type, out var subscriberList))
            {
                // Находим и удаляем конкретный делегат
                if (actionTargetMap.TryGetValue(action, out var actualSubscriber))
                {
                    subscriberList.Remove(actualSubscriber);
                    actionTargetMap.Remove(action); // Чистим карту
                    if (subscriberList.Count == 0)
                    {
                        _subscribers.Remove(type); // Если подписчиков больше нет, удаляем тип
                    }
                }
            }
        }
        // --- КОНЕЦ НОВОГО МЕТОДА ---

        // Добавим карту для хранения ссылок на делегаты
        private readonly Dictionary<Delegate, object> actionTargetMap = new Dictionary<Delegate, object>();

        public void Publish<T>(T message) where T : class
        {
            var messageType = message.GetType();

            // 1. СНАЧАЛА создаем КОПИЮ ключей (типов сообщений)
            var relevantKeys = _subscribers.Keys
                .Where(key => key.IsAssignableFrom(messageType))
                .ToList(); // <-- Делаем копию ключей

            // 2. Идем по КОПИИ ключей
            foreach (var subscriberType in relevantKeys)
            {
                // 3. Проверяем, существует ли этот тип ВСЁ ЕЩЁ в ОРИГИНАЛЬНОМ словаре
                //    (вдруг его удалили через Unsubscribe, пока мы шли по копии)
                if (_subscribers.TryGetValue(subscriberType, out var originalSubscriberList))
                {
                    // 4. Создаем КОПИЮ списка подписчиков для этого типа
                    var subscribersCopy = originalSubscriberList.ToList();

                    // 5. Идем по КОПИИ подписчиков
                    foreach (var subscriber in subscribersCopy)
                    {
                        // 6. (Опционально, но безопаснее) Проверяем, есть ли этот подписчик
                        //    ВСЁ ЕЩЁ в ОРИГИНАЛЬНОМ списке перед вызовом
                        if (originalSubscriberList.Contains(subscriber))
                        {
                            (subscriber as Delegate)?.DynamicInvoke(message);
                        }
                    }
                }
            }
        }
        // ... (твой CloseTabMessage, если он тут) ...
        public class CloseTabMessage //
        { //
            public TabViewModelBase TabToClose { get; set; } //

            public CloseTabMessage(TabViewModelBase tabToClose) //
            { //
                TabToClose = tabToClose; //
            } //
        } //
    }
}