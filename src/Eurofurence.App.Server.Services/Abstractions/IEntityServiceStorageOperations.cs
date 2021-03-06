﻿using System;
using System.Threading.Tasks;
using Eurofurence.App.Domain.Model;
using Eurofurence.App.Domain.Model.Sync;

namespace Eurofurence.App.Server.Services.Abstractions
{
    public interface IEntityServiceStorageOperations<TEntity> where TEntity : EntityBase
    {
        Task<DeltaResponse<TEntity>> GetDeltaResponseAsync(DateTime? minLastDateTimeChangedUtc = null);
        Task<EntityStorageInfoRecord> GetStorageInfoAsync();
        Task ResetStorageDeltaAsync();
    }
}