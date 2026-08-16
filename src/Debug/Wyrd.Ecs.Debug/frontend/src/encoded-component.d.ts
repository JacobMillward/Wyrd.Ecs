// Hand-written, not regenerated. Wyrd.Ecs.EncodedComponent has a custom JsonConverter
// (EncodedComponentJsonConverter) whose real output doesn't match what reflecting over
// the C# record would produce - it omits SchemaHash entirely and embeds Data as whatever
// arbitrary JSON the component's own codec produced, not a byte array. TypeGen's
// customTypeMappings (tgconfig.json) redirects references to this type but does not
// generate an import for it, so this is declared globally instead of as a module export -
// every generated file that references the bare name "EncodedComponent" resolves it here
// with no import needed. Entity itself is a real module export (not ambient), so this
// file still needs to import it despite declaring EncodedComponent globally.
import type { Entity } from './generated/entity';

declare global {
    interface EncodedComponent {
        entity: Entity;
        discriminator: string;
        data: unknown;
    }
}

export {};
