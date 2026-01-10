import { Button, Card, CardPreview, Heading, Text } from '@react-spectrum/s2'
import { useStore } from '@tanstack/react-store'
import { counterStore, incrementCounter } from '../../Client/store'

interface HomeProps {
    message: string
}

export default function Home({ message }: HomeProps) {
    const { count } = useStore(counterStore)

    return (
        <div className="grid gap-6 lg:grid-cols-12">
            <section className="lg:col-span-7">
                <Heading level={1}>{message}</Heading>
                <div className="mt-2 max-w-prose">
                    <Text>
                        This template is wired for Spectrum S2 (v1.0.0) + Tailwind, TanStack Router virtual
                        file routes, and TanStack Store.
                    </Text>
                </div>

                <div className="mt-6 flex flex-wrap items-center gap-3">
                    <Button onPress={incrementCounter}>Increment</Button>
                    <Text>Count: {count}</Text>
                </div>
            </section>

            <aside className="lg:col-span-5">
                <Card>
                    <CardPreview>
                        <div className="rounded-lg bg-gradient-to-br from-[color:var(--spectrum-gray-100)] to-[color:var(--spectrum-gray-200)] p-6">
                            <Heading level={3}>Next steps</Heading>
                            <div className="mt-2 space-y-2">
                                <Text>1) Edit `Features/Home/index.tsx`</Text>
                                <Text>2) Add routes via TanStack virtual file routes</Text>
                                <Text>3) Keep production as plain `wwwroot/dist` static files</Text>
                            </div>
                        </div>
                    </CardPreview>
                </Card>
            </aside>
        </div>
    )
}
