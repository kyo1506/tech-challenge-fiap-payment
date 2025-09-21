using Domain.Aggregates;
using Domain.Entities;
using Domain.QueryModels;
using JasperFx.Events;
using Marten;
using Marten.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Interfaces.Repositories;

public interface IWalletRepository 
{
    Task<UserWallet?> GetByUserIdAsync(Guid userId);
    Task StoreAsync(UserWallet wallet);
    Task<IReadOnlyList<HistoricEvent>> GetHistoryAsync(Guid userId);
}