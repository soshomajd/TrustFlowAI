import {
  ArrowRight,
  Check,
  FlaskConical,
  Minus,
  ShieldCheck,
} from "lucide-react";
import Link from "next/link";
import type { ReactNode } from "react";

import { EscrowPreview } from "@/features/landing/components/escrow-preview";
import { LandingHeader } from "@/features/landing/components/landing-header";
import { Reveal } from "@/features/landing/components/reveal";
import {
  aiFeatures,
  clientSteps,
  comparison,
  faqs,
  freelancerSteps,
  lifecycle,
  problems,
  rules,
  trustPoints,
  type Step,
} from "@/features/landing/content";

const primaryButton =
  "inline-flex min-h-12 items-center justify-center gap-2 rounded-xl bg-primary px-6 text-base font-semibold text-primary-foreground shadow-blue-glow transition-colors hover:bg-primary/85 focus-visible:ring-3 focus-visible:ring-ring/60 focus-visible:outline-none";

const secondaryButton =
  "inline-flex min-h-12 items-center justify-center gap-2 rounded-xl border border-border bg-card/60 px-6 text-base font-semibold transition-colors hover:border-primary/50 hover:bg-card focus-visible:ring-3 focus-visible:ring-ring/60 focus-visible:outline-none";

export function LandingPage() {
  return (
    <>
      <a
        href="#main"
        className="sr-only z-50 rounded-lg bg-primary px-4 py-3 text-primary-foreground focus:not-sr-only focus:fixed focus:left-4 focus:top-4"
      >
        Skip to main content
      </a>

      {/* Sections fade in with script. Without it they must still be there. */}
      <noscript>
        <style>{`[style*="opacity:0"]{opacity:1!important;transform:none!important}`}</style>
      </noscript>

      <LandingHeader />

      <main id="main">
        <Hero />
        <Problems />
        <HowItWorks />
        <Protection />
        <AiHelp />
        <Comparison />
        <Faq />
        <FinalCta />
      </main>

      <Footer />
    </>
  );
}

function Hero() {
  return (
    <section className="relative overflow-hidden">
      <div
        aria-hidden="true"
        className="absolute inset-0 -z-10 opacity-[0.35] [background-image:linear-gradient(to_right,rgb(139_152_170/0.12)_1px,transparent_1px),linear-gradient(to_bottom,rgb(139_152_170/0.12)_1px,transparent_1px)] [background-size:48px_48px] [mask-image:radial-gradient(ellipse_at_top,black_30%,transparent_72%)]"
      />

      <div className="mx-auto grid max-w-7xl items-center gap-14 px-4 pb-20 pt-14 sm:px-6 lg:grid-cols-[1.1fr_0.9fr] lg:gap-10 lg:pb-28 lg:pt-20">
        <div>
          <p className="inline-flex items-center gap-2 rounded-full border border-warning/30 bg-warning/10 px-3 py-1.5 text-xs font-medium text-warning">
            <FlaskConical className="size-3.5" aria-hidden="true" />
            Early access, running on a test network
          </p>

          <h1 className="mt-6 text-balance text-4xl font-bold leading-[1.08] tracking-tight sm:text-5xl lg:text-6xl">
            Hire with confidence.
            <span className="block bg-linear-to-r from-primary via-electric to-electric bg-clip-text text-transparent">
              Pay when the work is done.
            </span>
          </h1>

          <p className="mt-6 max-w-xl text-pretty text-lg leading-8 text-muted-foreground">
            TrustFlow locks your project budget in a smart contract and
            releases it milestone by milestone. If you cannot agree, an AI
            reads the evidence and a human decides. Nobody can touch the money
            in between.
          </p>

          <div className="mt-8 flex flex-col gap-3 sm:flex-row">
            <Link href="/register?role=Client" className={primaryButton}>
              Hire a freelancer
              <ArrowRight className="size-4" aria-hidden="true" />
            </Link>

            <Link href="/register?role=Freelancer" className={secondaryButton}>
              Find work
            </Link>
          </div>

          <ul className="mt-10 grid gap-3 sm:grid-cols-2">
            {trustPoints.map((point) => (
              <li
                key={point.label}
                className="flex items-center gap-3 text-sm text-foreground/90"
              >
                <span
                  aria-hidden="true"
                  className="flex size-9 shrink-0 items-center justify-center rounded-lg border border-primary/25 bg-primary/10 text-electric"
                >
                  <point.icon className="size-4.5" />
                </span>

                {point.label}
              </li>
            ))}
          </ul>
        </div>

        <Reveal delay={0.1}>
          <EscrowPreview />
        </Reveal>
      </div>
    </section>
  );
}

function SectionHeading({
  id,
  eyebrow,
  title,
  children,
}: {
  id: string;
  eyebrow: string;
  title: string;
  children?: ReactNode;
}) {
  return (
    <div className="mx-auto max-w-3xl text-center">
      <p className="text-xs font-semibold uppercase tracking-[0.25em] text-electric">
        {eyebrow}
      </p>

      <h2
        id={id}
        className="mt-3 text-balance text-3xl font-bold tracking-tight sm:text-4xl"
      >
        {title}
      </h2>

      {children && (
        <p className="mt-4 text-pretty text-base leading-7 text-muted-foreground">
          {children}
        </p>
      )}
    </div>
  );
}

function Problems() {
  return (
    <section
      aria-labelledby="problems-title"
      className="border-y border-border/60 bg-card/30 py-20"
    >
      <div className="mx-auto max-w-7xl px-4 sm:px-6">
        <Reveal>
          <SectionHeading
            id="problems-title"
            eyebrow="The old way"
            title="Somebody always has to trust somebody"
          >
            Hiring someone online usually means one side takes the risk. Here
            the rules take it instead.
          </SectionHeading>
        </Reveal>

        <div className="mt-12 grid gap-5 md:grid-cols-3">
          {problems.map((problem, index) => (
            <Reveal key={problem.after} delay={index * 0.06}>
              <article className="h-full rounded-2xl border bg-card/70 p-6">
                <span
                  aria-hidden="true"
                  className="flex size-11 items-center justify-center rounded-xl border border-primary/25 bg-primary/10 text-electric"
                >
                  <problem.icon className="size-5" />
                </span>

                <p className="mt-5 text-sm text-muted-foreground line-through decoration-muted-foreground/60">
                  {problem.before}
                </p>

                <h3 className="mt-1 text-xl font-semibold">{problem.after}</h3>

                <p className="mt-3 text-sm leading-6 text-muted-foreground">
                  {problem.detail}
                </p>
              </article>
            </Reveal>
          ))}
        </div>
      </div>
    </section>
  );
}

function StepList({
  title,
  steps,
  cta,
}: {
  title: string;
  steps: Step[];
  cta: ReactNode;
}) {
  return (
    <div className="rounded-2xl border bg-card/70 p-6 sm:p-8">
      <h3 className="text-xl font-semibold">{title}</h3>

      <ol className="mt-6 space-y-5">
        {steps.map((step, index) => (
          <li key={step.title} className="flex gap-4">
            <span
              aria-hidden="true"
              className="flex size-9 shrink-0 items-center justify-center rounded-full border border-primary/40 bg-primary/15 text-sm font-semibold text-electric"
            >
              {index + 1}
            </span>

            <div>
              <p className="font-medium">{step.title}</p>

              <p className="mt-1 text-sm leading-6 text-muted-foreground">
                {step.text}
              </p>
            </div>
          </li>
        ))}
      </ol>

      <div className="mt-8">{cta}</div>
    </div>
  );
}

function HowItWorks() {
  return (
    <section
      id="how-it-works"
      aria-labelledby="how-title"
      className="scroll-mt-20 py-20 sm:py-24"
    >
      <div className="mx-auto max-w-7xl px-4 sm:px-6">
        <Reveal>
          <SectionHeading
            id="how-title"
            eyebrow="How it works"
            title="Four steps, whichever side you are on"
          >
            One project, split into milestones. Money moves only when a
            milestone is done.
          </SectionHeading>
        </Reveal>

        <div className="mt-12 grid gap-6 lg:grid-cols-2">
          <Reveal>
            <StepList
              title="If you are hiring"
              steps={clientSteps}
              cta={
                <Link href="/register?role=Client" className={primaryButton}>
                  Start as a client
                  <ArrowRight className="size-4" aria-hidden="true" />
                </Link>
              }
            />
          </Reveal>

          <Reveal delay={0.08}>
            <StepList
              title="If you are working"
              steps={freelancerSteps}
              cta={
                <Link
                  href="/register?role=Freelancer"
                  className={secondaryButton}
                >
                  Start as a freelancer
                  <ArrowRight className="size-4" aria-hidden="true" />
                </Link>
              }
            />
          </Reveal>
        </div>
      </div>
    </section>
  );
}

function Protection() {
  return (
    <section
      id="protection"
      aria-labelledby="protection-title"
      className="scroll-mt-20 border-y border-border/60 bg-card/30 py-20 sm:py-24"
    >
      <div className="mx-auto max-w-7xl px-4 sm:px-6">
        <Reveal>
          <SectionHeading
            id="protection-title"
            eyebrow="Protection"
            title="Rules written into the contract, not into small print"
          >
            Every project gets its own escrow contract. What follows is what it
            allows and what it never does.
          </SectionHeading>
        </Reveal>

        <Reveal>
          <ol
            aria-label="Life of a milestone"
            className="mt-12 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6"
          >
            {lifecycle.map((item, index) => (
              <li
                key={item.label}
                className="relative rounded-xl border bg-card/70 p-4"
              >
                <span className="text-xs font-semibold text-electric">
                  {String(index + 1).padStart(2, "0")}
                </span>

                <p className="mt-1 font-semibold">{item.label}</p>

                <p className="mt-1 text-xs leading-5 text-muted-foreground">
                  {item.note}
                </p>
              </li>
            ))}
          </ol>
        </Reveal>

        <div className="mt-8 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {rules.map((rule, index) => (
            <Reveal key={rule.title} delay={(index % 3) * 0.05}>
              <article className="h-full rounded-2xl border bg-card/70 p-6">
                <span
                  aria-hidden="true"
                  className="flex size-10 items-center justify-center rounded-lg border border-success/30 bg-success/10 text-success"
                >
                  <rule.icon className="size-5" />
                </span>

                <h3 className="mt-4 text-lg font-semibold">{rule.title}</h3>

                <p className="mt-2 text-sm leading-6 text-muted-foreground">
                  {rule.text}
                </p>
              </article>
            </Reveal>
          ))}
        </div>
      </div>
    </section>
  );
}

function AiHelp() {
  return (
    <section
      id="ai-help"
      aria-labelledby="ai-title"
      className="scroll-mt-20 py-20 sm:py-24"
    >
      <div className="mx-auto max-w-7xl px-4 sm:px-6">
        <Reveal>
          <SectionHeading
            id="ai-title"
            eyebrow="AI help"
            title="An assistant that advises and never decides"
          >
            It helps you write, check and explain. It cannot sign, release or
            refund anything, and every answer says it can be wrong.
          </SectionHeading>
        </Reveal>

        <div className="mt-12 grid gap-5 md:grid-cols-3">
          {aiFeatures.map((feature, index) => (
            <Reveal key={feature.title} delay={index * 0.06}>
              <article className="h-full rounded-2xl border bg-card/70 p-6">
                <span
                  aria-hidden="true"
                  className="flex size-11 items-center justify-center rounded-xl border border-electric/30 bg-electric/10 text-electric"
                >
                  <feature.icon className="size-5" />
                </span>

                <h3 className="mt-5 text-lg font-semibold">{feature.title}</h3>

                <p className="mt-2 text-sm leading-6 text-muted-foreground">
                  {feature.text}
                </p>
              </article>
            </Reveal>
          ))}
        </div>
      </div>
    </section>
  );
}

function Comparison() {
  return (
    <section
      aria-labelledby="compare-title"
      className="border-y border-border/60 bg-card/30 py-20 sm:py-24"
    >
      <div className="mx-auto max-w-5xl px-4 sm:px-6">
        <Reveal>
          <SectionHeading
            id="compare-title"
            eyebrow="The difference"
            title="Trust the rules, not the platform"
          />
        </Reveal>

        <Reveal>
          <div className="mt-12 overflow-x-auto rounded-2xl border bg-card/70">
            <table className="w-full min-w-[34rem] border-collapse text-left text-sm">
              <caption className="sr-only">
                How TrustFlow differs from a typical freelance platform
              </caption>

              <thead>
                <tr className="border-b">
                  <th scope="col" className="p-4 font-medium text-muted-foreground">
                    <span className="sr-only">Topic</span>
                  </th>

                  <th scope="col" className="p-4 font-medium text-muted-foreground">
                    A typical platform
                  </th>

                  <th scope="col" className="p-4 font-semibold text-electric">
                    TrustFlow
                  </th>
                </tr>
              </thead>

              <tbody>
                {comparison.map((row) => (
                  <tr key={row.row} className="border-b last:border-b-0">
                    <th scope="row" className="p-4 align-top font-medium">
                      {row.row}
                    </th>

                    <td className="p-4 align-top text-muted-foreground">
                      <span className="flex gap-2">
                        <Minus
                          className="mt-0.5 size-4 shrink-0"
                          aria-hidden="true"
                        />
                        {row.typical}
                      </span>
                    </td>

                    <td className="p-4 align-top">
                      <span className="flex gap-2">
                        <Check
                          className="mt-0.5 size-4 shrink-0 text-success"
                          aria-hidden="true"
                        />
                        {row.trustflow}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Reveal>
      </div>
    </section>
  );
}

function Faq() {
  return (
    <section
      id="faq"
      aria-labelledby="faq-title"
      className="scroll-mt-20 py-20 sm:py-24"
    >
      <div className="mx-auto max-w-3xl px-4 sm:px-6">
        <Reveal>
          <SectionHeading
            id="faq-title"
            eyebrow="Questions"
            title="Straight answers"
          />
        </Reveal>

        <div className="mt-10 space-y-3">
          {faqs.map((item) => (
            <details
              key={item.question}
              className="group rounded-xl border bg-card/70 open:border-primary/40"
            >
              <summary className="flex min-h-12 cursor-pointer list-none items-center justify-between gap-4 rounded-xl px-5 py-3 font-medium outline-none focus-visible:ring-3 focus-visible:ring-ring/60 [&::-webkit-details-marker]:hidden">
                {item.question}

                <span
                  aria-hidden="true"
                  className="text-xl leading-none text-electric transition-transform group-open:rotate-45"
                >
                  +
                </span>
              </summary>

              <p className="px-5 pb-5 text-sm leading-6 text-muted-foreground">
                {item.answer}
              </p>
            </details>
          ))}
        </div>
      </div>
    </section>
  );
}

function FinalCta() {
  return (
    <section aria-labelledby="cta-title" className="px-4 pb-24 sm:px-6">
      <Reveal>
        <div className="relative mx-auto max-w-5xl overflow-hidden rounded-3xl border border-primary/30 bg-linear-to-br from-primary/20 via-card to-card p-8 text-center sm:p-14">
          <div
            aria-hidden="true"
            className="absolute -top-24 left-1/2 size-80 -translate-x-1/2 rounded-full bg-electric/15 blur-3xl"
          />

          <div className="relative">
            <ShieldCheck
              className="mx-auto size-10 text-electric"
              aria-hidden="true"
            />

            <h2
              id="cta-title"
              className="mt-5 text-balance text-3xl font-bold tracking-tight sm:text-4xl"
            >
              Try the whole flow with test tokens
            </h2>

            <p className="mx-auto mt-4 max-w-xl text-pretty text-muted-foreground">
              Create a project, fund an escrow, deliver work and release
              payment. It takes a few minutes and risks nothing.
            </p>

            <div className="mt-8 flex flex-col justify-center gap-3 sm:flex-row">
              <Link href="/register?role=Client" className={primaryButton}>
                Hire a freelancer
                <ArrowRight className="size-4" aria-hidden="true" />
              </Link>

              <Link href="/register?role=Freelancer" className={secondaryButton}>
                Find work
              </Link>
            </div>
          </div>
        </div>
      </Reveal>
    </section>
  );
}

function Footer() {
  return (
    <footer className="border-t border-border/60 py-10">
      <div className="mx-auto flex max-w-7xl flex-col items-center justify-between gap-4 px-4 text-sm text-muted-foreground sm:flex-row sm:px-6">
        <p className="flex items-center gap-2">
          <ShieldCheck className="size-4 text-electric" aria-hidden="true" />
          TrustFlow AI. Milestone escrow for freelance work.
        </p>

        <nav aria-label="Footer" className="flex items-center gap-1">
          <Link
            href="/login"
            className="flex min-h-11 items-center rounded-lg px-3 hover:text-foreground"
          >
            Sign in
          </Link>

          <Link
            href="/register"
            className="flex min-h-11 items-center rounded-lg px-3 hover:text-foreground"
          >
            Create account
          </Link>
        </nav>
      </div>
    </footer>
  );
}
