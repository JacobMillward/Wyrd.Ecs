const PALETTE_SIZE = 12;

// FNV-1a: cheap, deterministic, and spreads short strings (component names) across
// the palette better than a plain char-code sum would.
function hash(text: string): number {
    let h = 0x811c9dc5;
    for (let i = 0; i < text.length; i++) {
        h ^= text.charCodeAt(i);
        h = Math.imul(h, 0x01000193);
    }
    return h >>> 0;
}

// A bare hash % PALETTE_SIZE can let two unrelated names collide on the same slot
// (e.g. "Position" and "Velocity" both hash to slot 0). Assigning once per
// discriminator and probing forward past whatever's already taken guarantees every
// name in use this session gets its own color, as long as there are PALETTE_SIZE or
// fewer of them; past that it wraps back to the preferred slot and colors repeat, same
// as the plain hash would. Assignments live for the page session, not persisted.
//
// Slot occupancy is derived from assignedSlots' values rather than tracked in a second
// collection: PALETTE_SIZE is small enough that scanning is free, and there's nothing
// to keep in lockstep by convention.
const assignedSlots = new Map<string, number>();

function slotFor(discriminator: string): number {
    const cached = assignedSlots.get(discriminator);
    if (cached !== undefined) return cached;

    const taken = new Set(assignedSlots.values());
    let slot = hash(discriminator) % PALETTE_SIZE;
    for (let attempt = 0; attempt < PALETTE_SIZE && taken.has(slot); attempt++) {
        slot = (slot + 1) % PALETTE_SIZE;
    }
    assignedSlots.set(discriminator, slot);
    return slot;
}

// Same discriminator always maps to the same palette slot, so "Health" reads as one
// consistent color everywhere a component chip names it. See theme.css's
// --wyrd-component-N-* tokens for the actual (AAA-verified) per-slot colors.
export function colorForComponent(discriminator: string): preact.JSX.CSSProperties {
    const slot = slotFor(discriminator);
    return {
        background: `var(--wyrd-component-${slot}-bg)`,
        color: `var(--wyrd-component-${slot}-text)`,
        borderColor: `var(--wyrd-component-${slot}-border)`,
    };
}
