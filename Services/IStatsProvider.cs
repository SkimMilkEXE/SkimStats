using System;
using SkimStats.Models;

namespace SkimStats.Services;

// os-specific source of stats, linux version comes later
public interface IStatsProvider : IDisposable
{
    StatsSnapshot Read();
}
