import type { ComponentProps } from 'react'

type IconProps = ComponentProps<'svg'> & { title?: string }

function baseProps(props: IconProps) {
    const { title, className, ...rest } = props
    return {
        width: 20,
        height: 20,
        viewBox: '0 0 20 20',
        fill: 'none',
        xmlns: 'http://www.w3.org/2000/svg',
        className: className ?? 'h-5 w-5',
        ...rest,
        children: (
            <>
                {title ? <title>{title}</title> : null}
                {props.children}
            </>
        ),
    }
}

export function IconBell(props: IconProps) {
    return (
        <svg {...baseProps(props)}>
            <path
                d="M6.5 7.5a3.5 3.5 0 0 1 7 0v2.2c0 .7.3 1.4.8 1.9l.7.7H4l.7-.7c.5-.5.8-1.2.8-1.9V7.5Z"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinejoin="round"
            />
            <path
                d="M8 14.5a2 2 0 0 0 4 0"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
            />
        </svg>
    )
}

export function IconSearch(props: IconProps) {
    return (
        <svg {...baseProps(props)}>
            <path
                d="M9 15a6 6 0 1 1 0-12 6 6 0 0 1 0 12Z"
                stroke="currentColor"
                strokeWidth="1.5"
            />
            <path d="M13.5 13.5 17 17" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
        </svg>
    )
}

export function IconMenu(props: IconProps) {
    return (
        <svg {...baseProps(props)}>
            <path d="M3 5h14M3 10h14M3 15h14" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
        </svg>
    )
}

export function IconX(props: IconProps) {
    return (
        <svg {...baseProps(props)}>
            <path d="M5 5l10 10M15 5 5 15" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
        </svg>
    )
}

export function IconPlus(props: IconProps) {
    return (
        <svg {...baseProps(props)}>
            <path d="M10 4v12M4 10h12" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
        </svg>
    )
}

export function IconChevronRight(props: IconProps) {
    return (
        <svg {...baseProps(props)}>
            <path d="M8 5l5 5-5 5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
    )
}

