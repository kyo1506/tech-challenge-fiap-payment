﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Exceptions;

public class InsufficientBalanceException : DomainException
{
    public decimal CurrentBalance { get; }
    public decimal RequiredAmount { get; }

    public InsufficientBalanceException(string message)
        : base(message)
    {
    }

    public InsufficientBalanceException(decimal currentBalance, decimal requiredAmount)
        : base($"Insufficient balance. Available: {currentBalance}, Required: {requiredAmount}")
    {
        CurrentBalance = currentBalance;
        RequiredAmount = requiredAmount;
    }

    public InsufficientBalanceException(string message, decimal currentBalance, decimal requiredAmount)
        : base(message)
    {
        CurrentBalance = currentBalance;
        RequiredAmount = requiredAmount;
    }
}