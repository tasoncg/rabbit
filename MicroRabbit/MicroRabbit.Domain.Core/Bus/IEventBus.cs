﻿using System;
using MicroRabbit.Domain.Core.Commands;
using MicroRabbit.Domain.Core.Events;

namespace MicroRabbit.Domain.Core.Bus
{
	public interface IEventBus
	{
		Task SendCommand<T>(T Command) where T : Command;
		void Publish<T>(T @event) where T : Event;
		void Subscribe<T, TH>()
			where T : Event
			where TH : IEventHandler<T>;

	}
}

