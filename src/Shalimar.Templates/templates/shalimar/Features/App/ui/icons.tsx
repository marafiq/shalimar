import type { ComponentProps } from 'react'

type IconProps = ComponentProps<'svg'> & { title?: string }

function baseProps(props: IconProps) {
    const { title: _title, className, ...rest } = props
    return {
        width: 20,
        height: 20,
        viewBox: '0 0 20 20',
        fill: 'none',
        xmlns: 'http://www.w3.org/2000/svg',
        className: className ?? 'h-5 w-5',
        ...rest,
    }
}

export function IconBell(props: IconProps) {
    return (
        <svg {...baseProps(props)}>
            {props.title ? <title>{props.title}</title> : null}
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
            {props.title ? <title>{props.title}</title> : null}
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
            {props.title ? <title>{props.title}</title> : null}
            <path d="M3 5h14M3 10h14M3 15h14" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
        </svg>
    )
}

export function IconX(props: IconProps) {
    return (
        <svg {...baseProps(props)}>
            {props.title ? <title>{props.title}</title> : null}
            <path d="M5 5l10 10M15 5 5 15" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
        </svg>
    )
}

export function IconPlus(props: IconProps) {
    return (
        <svg {...baseProps(props)}>
            {props.title ? <title>{props.title}</title> : null}
            <path d="M10 4v12M4 10h12" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
        </svg>
    )
}

export function IconChevronRight(props: IconProps) {
    return (
        <svg {...baseProps(props)}>
            {props.title ? <title>{props.title}</title> : null}
            <path d="M8 5l5 5-5 5" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
    )
}

export function IconDashboard(props: IconProps) {
    return (
        <svg {...baseProps(props)}>
            {props.title ? <title>{props.title}</title> : null}
            <path
                d="M3.5 10a6.5 6.5 0 1 1 13 0"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
            />
            <path
                d="M10 10l3.5-2"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
                strokeLinejoin="round"
            />
            <path d="M10 10.75a.75.75 0 1 0 0-1.5.75.75 0 0 0 0 1.5Z" fill="currentColor" />
        </svg>
    )
}

export function IconTasks(props: IconProps) {
    return (
        <svg {...baseProps(props)}>
            {props.title ? <title>{props.title}</title> : null}
            <path
                d="M6.5 4.5h10M6.5 10h10M6.5 15.5h10"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
            />
            <path
                d="M3.5 4.7l.8.8 1.7-1.9M3.5 10.2l.8.8 1.7-1.9M3.5 15.7l.8.8 1.7-1.9"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
                strokeLinejoin="round"
            />
        </svg>
    )
}

export function IconBoard(props: IconProps) {
    return (
        <svg {...baseProps(props)}>
            {props.title ? <title>{props.title}</title> : null}
            <path
                d="M4.5 4.5h11a1 1 0 0 1 1 1v9a1 1 0 0 1-1 1h-11a1 1 0 0 1-1-1v-9a1 1 0 0 1 1-1Z"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinejoin="round"
            />
            <path
                d="M7 4.5v11M13 4.5v11M3.5 8h13"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinecap="round"
            />
        </svg>
    )
}

export function IconAccounts(props: IconProps) {
    return (
        <svg {...baseProps(props)}>
            {props.title ? <title>{props.title}</title> : null}
            <path
                d="M4.5 7.5h11V16H4.5V7.5Z"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinejoin="round"
            />
            <path d="M6.5 7.5V5.8c0-.7.6-1.3 1.3-1.3h4.4c.7 0 1.3.6 1.3 1.3v1.7" stroke="currentColor" strokeWidth="1.5" />
            <path d="M4.5 11h11" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
        </svg>
    )
}

export function IconSettings(props: IconProps) {
    return (
        <svg {...baseProps(props)}>
            {props.title ? <title>{props.title}</title> : null}
            <path
                d="M10 12.25a2.25 2.25 0 1 0 0-4.5 2.25 2.25 0 0 0 0 4.5Z"
                stroke="currentColor"
                strokeWidth="1.5"
            />
            <path
                d="M16.5 10a6.5 6.5 0 0 0-.1-1l1.2-.9-1.6-2.8-1.4.5a6.6 6.6 0 0 0-1.7-1l-.2-1.5H8.3l-.2 1.5a6.6 6.6 0 0 0-1.7 1L5 5.3 3.4 8.1l1.2.9a6.5 6.5 0 0 0 0 2l-1.2.9L5 14.7l1.4-.5a6.6 6.6 0 0 0 1.7 1l.2 1.5h3.4l.2-1.5a6.6 6.6 0 0 0 1.7-1l1.4.5 1.6-2.8-1.2-.9c.1-.3.1-.7.1-1Z"
                stroke="currentColor"
                strokeWidth="1.5"
                strokeLinejoin="round"
            />
        </svg>
    )
}

