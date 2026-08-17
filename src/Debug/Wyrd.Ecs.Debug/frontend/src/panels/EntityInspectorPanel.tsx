import { useEffect, useState } from 'preact/hooks';
import { css } from '@linaria/core';
import { snapshot, selectedEntity } from '../store';
import { entityEquals } from '../entityFormat';
import type { Entity } from '../generated/entity';
import type { InspectedComponent } from '../generated/inspected-component';

const grid = css`
    columns: 190px;
    column-gap: 10px;
    height: 100%;
    overflow: auto;
    padding: 8px;
`;

const card = css`
    break-inside: avoid;
    display: inline-block;
    width: 100%;
    border: 1px solid var(--wyrd-hairline);
    border-radius: 6px;
    background: var(--wyrd-bg-nav);
    margin-bottom: 10px;
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

const readout = css`
    font-family: ui-monospace, monospace;
    font-size: 11px;
    opacity: 0.75;
    width: 32px;
    text-align: right;
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

interface InspectorFieldViewProps {
    field: InspectorField;
    onChange: (label: string, value: unknown) => void;
}

// Draws one of the six InspectorField kinds. Group recurses, passing the same onChange
// straight through: edits are addressed by label alone (see inspector-field.d.ts), so
// there's nothing to merge/bubble at each nesting level.
function InspectorFieldView({ field, onChange }: InspectorFieldViewProps) {
    switch (field.kind) {
        case 'slider':
            return (
                <div class={fieldRow}>
                    <label class={fieldLabel}>{field.label}</label>
                    <div class={fieldControl}>
                        <input type="range" min={field.min} max={field.max} value={field.value} onInput={(e) => onChange(field.label, Number((e.target as HTMLInputElement).value))} />
                        <span class={readout}>{field.value}</span>
                    </div>
                </div>
            );
        case 'number':
            return (
                <div class={fieldRow}>
                    <label class={fieldLabel}>{field.label}</label>
                    <div class={fieldControl}>
                        <input class={`${textInput} ${numberInput}`} type="number" value={field.value} onChange={(e) => onChange(field.label, Number((e.target as HTMLInputElement).value))} />
                    </div>
                </div>
            );
        case 'text':
            return (
                <div class={fieldRow}>
                    <label class={fieldLabel}>{field.label}</label>
                    <div class={fieldControl}>
                        <input class={textInput} type="text" value={field.value} onChange={(e) => onChange(field.label, (e.target as HTMLInputElement).value)} />
                    </div>
                </div>
            );
        case 'checkbox':
            return (
                <div class={fieldRow}>
                    <label class={fieldLabel}>{field.label}</label>
                    <div class={fieldControl}>
                        <input type="checkbox" checked={field.value} onChange={(e) => onChange(field.label, (e.target as HTMLInputElement).checked)} />
                    </div>
                </div>
            );
        case 'readOnly':
            return (
                <div class={fieldRow}>
                    <label class={fieldLabel}>{field.label}</label>
                    <div class={fieldControl}>
                        <span class={readOnlyValue}>{field.value}</span>
                    </div>
                </div>
            );
        case 'group':
            return (
                <div class={`${fieldRow} ${fieldRowTop}`}>
                    <label class={fieldLabel}>{field.label}</label>
                    <div class={fieldControl}>
                        <div class={groupWrap}>
                            {field.children.map((child) => (
                                <InspectorFieldView key={child.label} field={child} onChange={onChange} />
                            ))}
                        </div>
                    </div>
                </div>
            );
    }
}

async function postRendererEdit(entity: Entity, discriminator: string, label: string, value: unknown): Promise<Response> {
    return fetch(`/api/entities/${entity.id}/${entity.generation}/components/${discriminator}/renderer-edit`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ label, value }),
    });
}

async function postFieldEdit(entity: Entity, discriminator: string, field: string, value: unknown): Promise<Response> {
    return fetch(`/api/entities/${entity.id}/${entity.generation}/components/${discriminator}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ field, value }),
    });
}

async function errorMessage(response: Response): Promise<string> {
    const body = (await response.json().catch(() => null)) as { error?: string } | null;
    return body?.error ?? 'Edit failed.';
}

export function EntityInspectorPanel() {
    const [errors, setErrors] = useState<Record<string, string>>({});
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

    async function handleRendererEdit(inspected: InspectedComponent, label: string, value: unknown) {
        const discriminator = inspected.component.discriminator;
        const response = await postRendererEdit(selected!, discriminator, label, value);
        setError(discriminator, response.ok ? null : await errorMessage(response));
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
        const response = await postFieldEdit(selected!, discriminator, field, value);
        setError(discriminator, response.ok ? null : await errorMessage(response));
    }

    return (
        <div class={grid}>
            {entitySnapshot.components.map((inspected) => {
                const discriminator = inspected.component.discriminator;
                const data = inspected.component.data as Record<string, unknown> | null;
                return (
                    <div key={discriminator} class={card}>
                        <h4 class={cardHeader}>{discriminator}</h4>
                        <div class={fieldsWrap}>
                            {inspected.field ? (
                                <InspectorFieldView field={inspected.field} onChange={(label, value) => handleRendererEdit(inspected, label, value)} />
                            ) : (
                                Object.entries(data ?? {}).map(([key, value]) => (
                                    <div key={key} class={fieldRow}>
                                        <label class={fieldLabel}>{key}</label>
                                        <div class={fieldControl}>
                                            <input
                                                class={textInput}
                                                type="text"
                                                value={JSON.stringify(value)}
                                                onChange={(e) => handleFieldEdit(inspected, key, (e.target as HTMLInputElement).value)}
                                            />
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
    );
}
