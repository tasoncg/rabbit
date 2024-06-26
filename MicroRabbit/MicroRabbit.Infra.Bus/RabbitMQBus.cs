﻿using System;
using MediatR;
using MicroRabbit.Domain.Core.Bus;
using MicroRabbit.Domain.Core.Commands;
using MicroRabbit.Domain.Core.Events;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Text;
using RabbitMQ.Client.Events;

namespace MicroRabbit.Infra.Bus
{
	public sealed class RabbitMQBus : IEventBus
	{
        private readonly IMediator _mediator;
        private readonly Dictionary<string, List<Type>> _handlers;
        private readonly List<Type> _eventTypes;
		public RabbitMQBus(IMediator mediator)
		{
            _mediator = mediator;
            _handlers = new Dictionary<string, List<Type>>();
            _eventTypes = new List<Type>();
		}

        public void Publish<T>(T @event) where T : Event
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            using(var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                var eventName = @event.GetType().Name;
                channel.QueueDeclare(eventName, false, false, false, null);
                var message = JsonConvert.SerializeObject(@event);
                var body = Encoding.UTF8.GetBytes(message);
                channel.BasicPublish("", eventName, null, body);
            }
        }

        public Task SendCommand<T>(T Command) where T : Command
        {
            throw new NotImplementedException();
        }

        public void Subscribe<T, TH>()
            where T : Event
            where TH : IEventHandler<T>
        {
            var eventName = typeof(T).Name;
            var handlerType = typeof(TH);

            if (!_eventTypes.Contains(typeof(T)))
            {
                _eventTypes.Add(typeof(T));
            }

            if (!_handlers.ContainsKey(eventName))
            {
                _handlers.Add(eventName, new List<Type>());
            }

            if (_handlers[eventName].Any(s=>s.GetType() == handlerType))
            {
                throw new ArgumentException($"Handler Type {handlerType.Name} is already registered for '{eventName}'", nameof(handlerType));
            }
            _handlers[eventName].Add(handlerType);
            StartBasicConsume<T>();
        }

        private void StartBasicConsume<T>() where T : Event
        {
            var factory = new ConnectionFactory()
            {
                HostName = "localhost",
                DispatchConsumersAsync = true
            };
            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();
            var eventName = typeof(T).Name;
            channel.QueueDeclare(eventName, false, false, false, null);
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += Consumer_Received;

            channel.BasicConsume(eventName, true, consumer);
        }

        private async Task Consumer_Received(object sender, BasicDeliverEventArgs e)
        {
            var eventname = e.RoutingKey;
            var message = Encoding.UTF8.GetString(e.Body.ToArray());
            try
            {
                await ProcessEvent(eventname, message).ConfigureAwait(false);
            }
            catch(Exception ex)
            {

            }

        }

        private async Task ProcessEvent(string eventname, string message)
        {
            if (_handlers.ContainsKey(eventname))
            {
                var subscriptions = _handlers[eventname];
                foreach(var subscription in subscriptions)
                {
                    var handler = Activator.CreateInstance(subscription);
                    if (handler == null) continue;
                    var eventType = _eventTypes.SingleOrDefault(t => t.Name == eventname);
                    var @event = JsonConvert.DeserializeObject(message, eventType!);
                    var conreteType = typeof(IEventHandler<>).MakeGenericType(eventType!);
                    await (Task)conreteType.GetMethod("Handle").Invoke(handler, new object[] { @event });
                }
            }
        }
    }
}

