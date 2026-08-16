using TypeGen.Core.SpecGeneration;
using Wyrd.Ecs.Debug.Internal;

namespace Wyrd.Ecs.Debug;

internal sealed class TypeScriptGenerationSpec : GenerationSpec
{
    public TypeScriptGenerationSpec()
    {
        AddInterface<ArchetypeSnapshot>();
        AddInterface<EntitySnapshot>();
        AddInterface<Entity>();
        AddInterface<ChangeLogEntry>();
        AddEnum<ChangeKind>();
        AddInterface<WorldSnapshot>();
    }
}
