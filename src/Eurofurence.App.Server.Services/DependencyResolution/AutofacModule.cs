﻿using Autofac;
using Eurofurence.App.Server.Services.Abstractions;
using Eurofurence.App.Server.Services.Events;
using Eurofurence.App.Server.Services.Storage;

namespace Eurofurence.App.Server.Services.DependencyResolution
{
    public class AutofacModule : Module
    {
        protected override void Load(ContainerBuilder moduleBuilder)
        {
            moduleBuilder.RegisterType<StorageServiceFactory>().As<IStorageServiceFactory>();
            moduleBuilder.RegisterType<EventService>().As<IEventService>();
            moduleBuilder.RegisterType<EventConferenceTrackService>().As<IEventConferenceTrackService>();
            moduleBuilder.RegisterType<EventConferenceRoomService>().As<IEventConferenceRoomService>();
            moduleBuilder.RegisterType<EventConferenceDayService>().As<IEventConferenceDayService>();
        }
    }
}
