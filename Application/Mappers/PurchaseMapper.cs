using Domain.Aggregates;
using Domain.Entities;
using Shared.DTOs.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Mappers;

public static class PurchaseMapper
{
    public static PurchaseResponse ToResponse(this Purchase purchase, decimal newBalance)
    {
        return new PurchaseResponse
        {
            PurchaseId = purchase.Id,
            Status = purchase.Status.ToString(),
            TotalPrice = purchase.TotalPrice,
            NewBalance = newBalance, 
            Items = purchase.Items.Select(item => new PurchaseItemResponse
            {
                GameId = item.GameId,
                PricePaid = item.FinalPrice
            }).ToList()
        };
    }
}
