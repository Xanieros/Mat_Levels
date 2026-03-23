using System;
using System.Collections.Generic;
using System.Text;

namespace MatLevels;
public record ItemLevelData
{
    public required string job { get; init; }
    public required int level { get; init;  }
}
