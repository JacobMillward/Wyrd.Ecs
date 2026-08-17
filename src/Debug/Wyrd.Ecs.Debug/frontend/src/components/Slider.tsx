import { useState } from 'preact/hooks';
import { css } from '@linaria/core';
import { useLiveValue } from './useLiveValue';

const wrap = css`
    display: flex;
    align-items: center;
    gap: 6px;
    flex: 1;
    min-width: 0;
`;

// min-width: 0 overrides the default min-width: auto, which otherwise stops a range
// input from shrinking below its ~129px intrinsic width.
const range = css`
    flex: 1;
    min-width: 0;
    width: 100%;
`;

const number = css`
    flex: none;
    width: 56px;
    background: var(--wyrd-bg);
    color: var(--wyrd-text);
    border: 1px solid var(--wyrd-chip-border);
    border-radius: 4px;
    padding: 3px 6px;
    font-size: 12px;
`;

export interface SliderProps {
    value: number;
    min: number;
    max: number;
    step?: number;
    // Decimal places to show in the number field while it's not focused. JS drops
    // trailing zeros when a number is stringified (3.0 renders as "3"), so a fractional
    // step needs this to keep its precision visible while dragging the range.
    decimals?: number;
    onCommit: (value: number) => void;
    class?: string;
}

// Range and number inputs share one live value (see useLiveValue) so dragging and
// typing stay in sync.
export function Slider({ value, min, max, step, decimals = 0, onCommit, class: className }: SliderProps) {
    const live = useLiveValue(value);
    const [numberFocused, setNumberFocused] = useState(false);
    const numberValue = decimals > 0 && !numberFocused ? live.value.toFixed(decimals) : live.value;
    return (
        <div class={className ? `${wrap} ${className}` : wrap}>
            <input
                class={range}
                type="range"
                min={min}
                max={max}
                step={step}
                value={live.value}
                onFocus={live.onFocus}
                onBlur={live.onBlur}
                onInput={(e) => {
                    const next = Number((e.target as HTMLInputElement).value);
                    live.setValue(next);
                    onCommit(next);
                }}
            />
            <input
                class={number}
                type="number"
                min={min}
                max={max}
                step={step}
                value={numberValue}
                onFocus={() => {
                    live.onFocus();
                    setNumberFocused(true);
                }}
                onInput={(e) => live.setValue(Number((e.target as HTMLInputElement).value))}
                onBlur={(e) => {
                    live.onBlur();
                    setNumberFocused(false);
                    onCommit(Number((e.target as HTMLInputElement).value));
                }}
            />
        </div>
    );
}
