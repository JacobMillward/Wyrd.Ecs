import { css, cx } from '@linaria/core';

const wrapper = css`
    display: inline-flex;
    width: 15px;
    height: 15px;

    svg {
        width: 100%;
        height: 100%;
        display: block;
    }
`;

export interface IconProps {
    svg: string;
    class?: string;
}

// lucide-static icons are raw <svg> markup strings (stroke="currentColor", so they
// inherit the wrapping element's text color). dangerouslySetInnerHTML is the standard
// way to drop that markup into a Preact tree without re-parsing it as JSX.
export function Icon({ svg, class: className }: IconProps) {
    return <span class={cx(wrapper, className)} dangerouslySetInnerHTML={{ __html: svg }} />;
}
