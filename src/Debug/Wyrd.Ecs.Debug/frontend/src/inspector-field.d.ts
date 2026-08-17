// Hand-written, not TypeGen-generated: InspectorField (Wyrd.Ecs.Debug.Abstractions)
// uses [JsonPolymorphic], which TypeGen's reflection-based generator has no special
// knowledge of, same reason encoded-component.d.ts is hand-written. Declared globally
// (not a module export) because tgconfig.json's customTypeMappings redirects
// generated references to the bare name "InspectorField" but does not add an import
// for it, same mechanism and caveat as EncodedComponent.
//
// Wire shape confirmed against the C# JsonDerivedType attributes and DebugServer's
// camelCase naming policy: each variant discriminates on "kind", matching
// JsonPolymorphicAttribute's TypeDiscriminatorPropertyName.
declare global {
    type InspectorField =
        | { kind: 'slider'; label: string; value: number; min: number; max: number }
        | { kind: 'number'; label: string; value: number }
        | { kind: 'text'; label: string; value: string }
        | { kind: 'checkbox'; label: string; value: boolean }
        | { kind: 'readOnly'; label: string; value: string }
        | { kind: 'group'; label: string; children: InspectorField[] };
}

export {};
