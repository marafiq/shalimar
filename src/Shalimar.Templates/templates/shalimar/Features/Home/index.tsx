interface HomeProps {
    message: string
}

export default function Home({ message }: HomeProps) {
    return (
        <div>
            <h1>{message}</h1>
            <p>Edit Features/Home/index.tsx to get started.</p>
        </div>
    )
}
