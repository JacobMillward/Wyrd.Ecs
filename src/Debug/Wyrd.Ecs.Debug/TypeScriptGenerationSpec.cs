using TypeGen.Core.SpecGeneration;
using Wyrd.Ecs.Debug.Internal;

namespace Wyrd.Ecs.Debug;

internal sealed class TypeScriptGenerationSpec : GenerationSpec
{
    public TypeScriptGenerationSpec()
    {
        AddInterface<ArchetypeSnapshot>();
        AddInterface<Entity>();
        AddInterface<InspectedComponent>();
        AddInterface<InspectedEntity>();
        AddInterface<ChangeLogEntry>();
        AddEnum<ChangeKind>();
        AddInterface<WorldSnapshot>();
        AddInterface<PlaybackSnapshot>();
    }
}
