import { useEffect, useState } from 'preact/hooks';
import { css } from '@linaria/core';
import { snapshot, selectedEntity } from '../store';
import { entityEquals } from '../entityFormat';
import { useLiveValue } from '../components/useLiveValue';
import { Slider } from '../components/Slider';
import type { Entity } from '../generated/entity';
import type { InspectedComponent } from '../generated/inspected-component';

// CSS multi-column only ever adds more ~260px columns as a container widens; it never
// widens the columns themselves to use leftover space, so with just two or three cards
// every column would stay pinned near the floor even in a very wide panel, starving a
// Group field's nested control regardless of how much room is actually available.
// Grid's 1fr tracks avoid that: minmax(260px, 1fr) keeps the same floor, but leftover
// width gets redistributed across whatever columns exist instead of sitting unused.
// align-content: start stops rows from stretching to fill extra height.
//
// The 260px floor still matters if the panel itself is narrower than one column: the
// track's minimum forces this container wider than its parent, which overflows and
// gets clipped/scrolled there, a contained and well understood failure mode, instead of
// silently narrowing every card back into the starved layout the floor exists to avoid.
const grid = css`
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
    align-content: start;
    gap: 10px;
    height: 100%;
    overflow: auto;
    padding: 8px;
`;

const card = css`
    border: 1px solid var(--wyrd-hairline);
    border-radius: 6px;
    background: var(--wyrd-bg-nav);
`;

const cardHeader = css`
    margin: 0;
    padding: 7px 10px;
    font-size: 12px;
    font-weight: 600;
    color: var(--wyrd-text);
    border-bottom: 1px solid var(--wyrd-hairline);
    font-family: ui-monospace, 'JetBrains Mono', Menlo, monospace;
`;

const fieldsWrap = css`
    padding: 8px 10px;
    display: flex;
    flex-direction: column;
    gap: 8px;
`;

const fieldRow = css`
    display: flex;
    align-items: center;
    gap: 8px;
    font-size: 12px;
    color: var(--wyrd-text);
`;

const fieldRowTop = css`
    align-items: flex-start;
`;

const fieldLabel = css`
    flex: 0 0 84px;
    opacity: 0.7;
`;

// A Group's children sit inside its indent guide, so they don't need the top-level
// label's full width; repeating 84px at each nesting depth is what starved the
// deepest control down to nothing in the first place. overflow-wrap covers the one
// long label in the demo ("Invulnerable"), which no longer fits on one line here.
const fieldLabelNested = css`
    flex: 0 0 60px;
    opacity: 0.7;
    overflow-wrap: break-word;
`;

const fieldControl = css`
    flex: 1;
    min-width: 0;
    display: flex;
    align-items: center;
    gap: 6px;
`;

const groupWrap = css`
    border-left: 2px solid var(--wyrd-chip-border);
    padding-left: 10px;
    display: flex;
    flex-direction: column;
    gap: 8px;
    width: 100%;
`;

const textInput = css`
    background: var(--wyrd-bg);
    color: var(--wyrd-text);
    border: 1px solid var(--wyrd-chip-border);
    border-radius: 4px;
    padding: 3px 6px;
    font-size: 12px;
    width: 100%;
`;

const numberInput = css`
    flex: none;
    width: 70px;
`;

const readOnlyValue = css`
    font-family: ui-monospace, monospace;
    opacity: 0.8;
`;

const fieldError = css`
    font-size: 11px;
    color: var(--wyrd-red);
    padding: 2px 10px 8px;
`;

const emptyState = css`
    padding: 16px;
    opacity: 0.6;
    font-size: 12px;
`;

interface FieldProps<TField> {
    field: TField;
    labelClass: string;
    onChange: (label: string, value: unknown) => void;
}

function SliderField({ field, labelClass, onChange }: FieldProps<Extract<InspectorField, { kind: 'slider' }>>) {
    return (
        <div class={fieldRow}>
            <label class={labelClass}>{field.label}</label>
            <div class={fieldControl}>
                <Slider value={field.value} min={field.min} max={field.max} onCommit={(next) => onChange(field.label, next)} />
            </div>
        </div>
    );
}

function NumberField({ field, labelClass, onChange }: FieldProps<Extract<InspectorField, { kind: 'number' }>>) {
    const live = useLiveValue(field.value);
    return (
        <div class={fieldRow}>
            <label class={labelClass}>{field.label}</label>
            <div class={fieldControl}>
                <input
                    class={`${textInput} ${numberInput}`}
                    type="number"
                    value={live.value}
                    onFocus={live.onFocus}
                    onInput={(e) => live.setValue(Number((e.target as HTMLInputElement).value))}
                    onBlur={(e) => {
                        live.onBlur();
                        onChange(field.label, Number((e.target as HTMLInputElement).value));
                    }}
                />
            </div>
        </div>
    );
}

function TextField({ field, labelClass, onChange }: FieldProps<Extract<InspectorField, { kind: 'text' }>>) {
    const live = useLiveValue(field.value);
    return (
        <div class={fieldRow}>
            <label class={labelClass}>{field.label}</label>
            <div class={fieldControl}>
                <input
                    class={textInput}
                    type="text"
                    value={live.value}
                    onFocus={live.onFocus}
                    onInput={(e) => live.setValue((e.target as HTMLInputElement).value)}
                    onBlur={(e) => {
                        live.onBlur();
                        onChange(field.label, (e.target as HTMLInputElement).value);
                    }}
                />
            </div>
        </div>
    );
}

function CheckboxField({ field, labelClass, onChange }: FieldProps<Extract<InspectorField, { kind: 'checkbox' }>>) {
    const live = useLiveValue(field.value);
    return (
        <div class={fieldRow}>
            <label class={labelClass}>{field.label}</label>
            <div class={fieldControl}>
                <input
                    type="checkbox"
                    checked={live.value}
                    onFocus={live.onFocus}
                    onBlur={live.onBlur}
                    onChange={(e) => {
                        const next = (e.target as HTMLInputElement).checked;
                        live.setValue(next);
                        onChange(field.label, next);
                    }}
                />
            </div>
        </div>
    );
}

interface InspectorFieldTreeProps {
    field: InspectorField;
    inspected: InspectedComponent;
    onCommit: (inspected: InspectedComponent, label: string, value: unknown) => void;
}

// Entry point for a card's field tree: builds the onChange adapter once per card, then
// hands off to the recursive renderer below, which passes that same function through
// unchanged at every nesting level. Takes inspected/onCommit as plain props, same shape
// as RawFieldInput.
function InspectorFieldTree({ field, inspected, onCommit }: InspectorFieldTreeProps) {
    return <InspectorFieldView field={field} onChange={(label, value) => onCommit(inspected, label, value)} />;
}

interface InspectorFieldViewProps {
    field: InspectorField;
    onChange: (label: string, value: unknown) => void;
    // True for a Group's children: they get the narrower fieldLabelNested instead of
    // repeating the top-level label width at every nesting depth.
    nested?: boolean;
}

// Dispatches to one of the six InspectorField kinds. Group recurses, passing the same
// onChange straight through: edits are addressed by label alone (see
// inspector-field.d.ts), so there's nothing to merge/bubble at each nesting level.
// Each editable kind is its own component (not inlined here) so useLiveValue is called
// unconditionally at that component's top level, not from inside this switch.
function InspectorFieldView({ field, onChange, nested = false }: InspectorFieldViewProps) {
    const labelClass = nested ? fieldLabelNested : fieldLabel;
    switch (field.kind) {
        case 'slider':
            return <SliderField field={field} labelClass={labelClass} onChange={onChange} />;
        case 'number':
            return <NumberField field={field} labelClass={labelClass} onChange={onChange} />;
        case 'text':
            return <TextField field={field} labelClass={labelClass} onChange={onChange} />;
        case 'checkbox':
            return <CheckboxField field={field} labelClass={labelClass} onChange={onChange} />;
        case 'readOnly':
            return (
                <div class={fieldRow}>
                    <label class={labelClass}>{field.label}</label>
                    <div class={fieldControl}>
                        <span class={readOnlyValue}>{field.value}</span>
                    </div>
                </div>
            );
        case 'group':
            return (
                <div class={`${fieldRow} ${fieldRowTop}`}>
                    <label class={labelClass}>{field.label}</label>
                    <div class={fieldControl}>
                        <div class={groupWrap}>
                            {field.children.map((child) => (
                                <InspectorFieldView key={child.label} field={child} onChange={onChange} nested />
                            ))}
                        </div>
                    </div>
                </div>
            );
    }
}

// Same live-value race as the InspectorField kinds above, for the components with no
// custom renderer: raw JSON text edited straight against the entity's field data. Takes
// inspected/field as plain props since it's the one place that already has both on hand
// for every entry in the fields loop below.
function RawFieldInput({
    inspected,
    field,
    value,
    onCommit,
}: {
    inspected: InspectedComponent;
    field: string;
    value: unknown;
    onCommit: (inspected: InspectedComponent, field: string, rawText: string) => void;
}) {
    const live = useLiveValue(JSON.stringify(value));
    return (
        <input
            class={textInput}
            type="text"
            value={live.value}
            onFocus={live.onFocus}
            onInput={(e) => live.setValue((e.target as HTMLInputElement).value)}
            onBlur={(e) => {
                live.onBlur();
                onCommit(inspected, field, (e.target as HTMLInputElement).value);
            }}
        />
    );
}

function editUrl(entity: Entity, discriminator: string, suffix = ''): string {
    return `/api/entities/${entity.id}/${entity.generation}/components/${discriminator}${suffix}`;
}

function postEdit(url: string, body: unknown): Promise<Response> {
    return fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
    });
}

async function errorMessage(response: Response): Promise<string> {
    const body = (await response.json().catch(() => null)) as { error?: string } | null;
    return body?.error ?? 'Edit failed.';
}

export function EntityInspectorPanel() {
    const [errors, setErrors] = useState<Record<string, string>>({});
    const [showSystemManaged, setShowSystemManaged] = useState(false);
    const selected = selectedEntity.value;

    // New selection: old field-level errors no longer apply to whatever's now shown.
    useEffect(() => {
        setErrors({});
    }, [selected?.id, selected?.generation]);

    if (selected === null) {
        return <p class={emptyState}>No entity selected.</p>;
    }

    const entitySnapshot = snapshot.value?.entities.find((e) => entityEquals(selected, e.entity)) ?? null;
    if (entitySnapshot === null) {
        return <p class={emptyState}>Entity no longer exists.</p>;
    }

    function setError(discriminator: string, message: string | null) {
        setErrors((prev) => {
            const next = { ...prev };
            if (message === null) delete next[discriminator];
            else next[discriminator] = message;
            return next;
        });
    }

    // Shared by both edit paths below: POST the request, then report the result the
    // same way either one reads (a per-discriminator error, cleared on success).
    async function submitEdit(discriminator: string, request: () => Promise<Response>) {
        const response = await request();
        setError(discriminator, response.ok ? null : await errorMessage(response));
    }

    async function handleRendererEdit(inspected: InspectedComponent, label: string, value: unknown) {
        const discriminator = inspected.component.discriminator;
        await submitEdit(discriminator, () => postEdit(editUrl(selected!, discriminator, '/renderer-edit'), { label, value }));
    }

    async function handleFieldEdit(inspected: InspectedComponent, field: string, rawText: string) {
        const discriminator = inspected.component.discriminator;
        let value: unknown;
        try {
            value = JSON.parse(rawText);
        } catch {
            setError(discriminator, `Invalid JSON for ${field}.`);
            return;
        }
        await submitEdit(discriminator, () => postEdit(editUrl(selected!, discriminator), { field, value }));
    }

    const systemManagedCount = entitySnapshot.components.filter((c) => c.isSystemManaged).length;
    const visibleComponents = entitySnapshot.components.filter((c) => showSystemManaged || !c.isSystemManaged);

    return (
        <div>
            {systemManagedCount > 0 && (
                <button onClick={() => setShowSystemManaged((v) => !v)}>
                    {showSystemManaged ? `Hide ${systemManagedCount} system-managed` : `Show ${systemManagedCount} system-managed`}
                </button>
            )}
            <div class={grid}>
                {visibleComponents.map((inspected) => {
                    const discriminator = inspected.component.discriminator;
                    const data = inspected.component.data as Record<string, unknown> | null;
                    return (
                        <div key={discriminator} class={card}>
                            <h4 class={cardHeader}>{discriminator}</h4>
                            <div class={fieldsWrap}>
                                {inspected.field ? (
                                    <InspectorFieldTree field={inspected.field} inspected={inspected} onCommit={handleRendererEdit} />
                                ) : (
                                    Object.entries(data ?? {}).map(([key, value]) => (
                                        <div key={key} class={fieldRow}>
                                            <label class={fieldLabel}>{key}</label>
                                            <div class={fieldControl}>
                                                <RawFieldInput inspected={inspected} field={key} value={value} onCommit={handleFieldEdit} />
                                            </div>
                                        </div>
                                    ))
                                )}
                            </div>
                            {errors[discriminator] && <div class={fieldError}>{errors[discriminator]}</div>}
                        </div>
                    );
                })}
            </div>
        </div>
    );
}
