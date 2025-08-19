global using System.Collections.Generic;
global using System;
global using System.Diagnostics;
global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;
global using System.Buffers;
global using EcsID = ulong;
global using IIntoSystemConfigs = Verse.ECS.Scheduling.Configs.IIntoNodeConfigs<Verse.ECS.Systems.ISystem>;
global using IIntoSystemSetConfigs = Verse.ECS.Scheduling.Configs.IIntoNodeConfigs<Verse.ECS.Systems.ISystemSet>;