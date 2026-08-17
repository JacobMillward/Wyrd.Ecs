import { useEffect, useState } from 'preact/hooks';

// Ignores liveValue while focused (so a live-updating source can't fight local
// editing), resyncs to it once focus is lost.
export function useLiveValue<T>(liveValue: T) {
    const [draft, setDraft] = useState(liveValue);
    const [focused, setFocused] = useState(false);

    useEffect(() => {
        if (!focused) setDraft(liveValue);
    }, [liveValue, focused]);

    return {
        value: draft,
        setValue: setDraft,
        onFocus: () => setFocused(true),
        onBlur: () => setFocused(false),
    };
}
