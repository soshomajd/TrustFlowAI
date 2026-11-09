"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { getEscrowStages, prepareNextEscrowStage } from "@/features/escrow/api/escrow-api";
import { useStartWork } from "@/features/escrow/hooks/use-start-work";
import { fundStagedMilestone } from "@/features/escrow/lib/fund-staged-milestone";
import { invalidateEscrowWorkflow } from "@/features/escrow/queries/invalidate-escrow-workflow";
import type { ProjectEscrow } from "@/features/escrow/types/escrow";
import { getProjectMilestones } from "@/features/milestones/api/milestones-api";
import { formatMoney } from "@/lib/money/exact-money";
import { getWalletErrorMessage } from "@/lib/wallet/get-wallet-error-message";
import { useState, type ReactNode } from "react";
import type { EscrowFundingStage } from "@/features/escrow/types/escrow";

export function StagedScopePanel({ projectId, escrow, viewerRole, renderFundingControls }: {
  projectId: string; escrow: ProjectEscrow; viewerRole: "Client" | "Freelancer";
  renderFundingControls: (stage: EscrowFundingStage) => ReactNode;
}) {
  const queryClient = useQueryClient();
  const [title, setTitle] = useState("");
  const [amount, setAmount] = useState("");
  const [deadline, setDeadline] = useState("");
  const stagesQuery = useQuery({ queryKey: ["escrow-stages", projectId, escrow.freelancerId],
    queryFn: () => getEscrowStages(projectId, escrow.freelancerId), refetchInterval: 5000 });
  const milestonesQuery = useQuery({ queryKey: ["escrow-next-plan", projectId],
    queryFn: () => getProjectMilestones(projectId), enabled: viewerRole === "Client", refetchInterval: 15000 });
  const start = useStartWork(projectId, escrow.freelancerId);
  const funding = useMutation({
    mutationFn: async (input: string | { title: string; amount: string; deadline: string }) => {
      const prepared = await prepareNextEscrowStage(projectId, escrow.freelancerId,
        typeof input === "string" ? input : "00000000-0000-0000-0000-000000000000", typeof input === "string" ? undefined : input);
      const reserved = (await getEscrowStages(projectId, escrow.freelancerId)).find(s => s.milestoneId === prepared.milestoneId);
      if (!reserved) throw new Error("The next funding scope is unavailable. Refresh and retry.");
      return fundStagedMilestone(projectId, escrow, reserved);
    },
    onSettled: async () => {
      await invalidateEscrowWorkflow(queryClient, projectId);
      await stagesQuery.refetch();
    },
  });
  const stages = stagesQuery.data ?? [];
  const current = stages.filter(s => s.chainIndex !== null).at(-1);
  const next = stages.find(s => s.chainIndex === null && s.reservationActive);
  const candidates = next ? [{ id: next.milestoneId, title: next.title, amountExact: next.amountExact }]
    : (milestonesQuery.data ?? []).filter(m => !m.freelancerId && m.status === "Pending");
  return <section className="mt-5 space-y-3 border-t pt-5" aria-label="Milestone funding and acceptance">
    <h3 className="font-medium">Milestone funding and acceptance</h3>
    {stagesQuery.isLoading && <p className="text-sm text-muted-foreground">Loading funded scope…</p>}
    {stagesQuery.isError && <p role="alert">The milestone plan could not be loaded. Refresh on-chain status and retry.</p>}
    {current && <div className="rounded-lg border p-3 text-sm">
      <p className="font-medium">{current.title} · {formatMoney(current.amountExact)}</p>
      <p className="mt-1 text-muted-foreground">{!current.reservationActive ? "Contract ended" :
        !current.fundedAt ? "Reserved · awaiting client funding" : !current.startedAt ? "Funded · waiting for freelancer acceptance" : "Accepted and started"}</p>
      {viewerRole === "Freelancer" && escrow.status === "Funded" && current.fundedAt && !current.startedAt &&
        <Button className="mt-3" disabled={start.isPending} onClick={() => start.mutate({
          contractAddress: escrow.contractAddress!, chainId: escrow.chainId, stage: current,
        })}>{start.isPending ? "Confirm in your wallet…" : "Accept and start"}</Button>}
    </div>}
    {stages.filter(s => s.chainIndex === null && s.reservationActive).map(s => <p key={s.id} className="text-sm text-muted-foreground">
      {s.title} · {formatMoney(s.amountExact)} · Planned, not funded
    </p>)}
    {viewerRole === "Client" && escrow.status === "Completed" && <div className="space-y-2">
      <p className="text-sm text-muted-foreground">The funded work is settled. Fund the next milestone in this same contract.</p>
      {(!current || current.fundedAt) && candidates.map(stage => <div key={stage.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border p-3 text-sm">
        <span>{stage.title} · {formatMoney(stage.amountExact ?? "0")}</span>
        <Button size="sm" disabled={funding.isPending} onClick={() => funding.mutate(stage.id)}>Fund this milestone</Button>
      </div>)}
      {current && !current.fundedAt && renderFundingControls(current)}
      {current?.fundedAt && !next && <form className="space-y-3 rounded-lg border p-3" onSubmit={event => {
        event.preventDefault();
        funding.mutate({ title, amount, deadline: new Date(`${deadline}T00:00:00`).toISOString() });
      }}>
        <p className="text-sm font-medium">Add and fund new work in this contract</p>
        <label className="block text-sm">Milestone title<input className="mt-1 block w-full rounded-md border bg-background p-2"
          required maxLength={200} value={title} onChange={event => setTitle(event.target.value)} /></label>
        <label className="block text-sm">Amount<input className="mt-1 block w-full rounded-md border bg-background p-2"
          required type="number" min="0.01" step="0.01" value={amount} onChange={event => setAmount(event.target.value)} /></label>
        <label className="block text-sm">Deadline within the project deadline<input className="mt-1 block w-full rounded-md border bg-background p-2"
          required type="date" value={deadline} onChange={event => setDeadline(event.target.value)} /></label>
        <Button disabled={funding.isPending} type="submit">Add and fund milestone</Button>
        <p className="text-xs text-muted-foreground">This increases the project plan by this amount. The milestone remains unfunded until your wallet deposit succeeds.</p>
      </form>}
    </div>}
    {(funding.isError || start.isError) && <p role="alert" className="text-sm text-destructive">
      {getWalletErrorMessage(funding.error ?? start.error, "The operation could not be completed. Refresh and retry.")}
    </p>}
    <p className="text-xs text-muted-foreground">Each freelancer has a separate contract. Only fully funded work can start. Later milestones reuse this contract and require a new deposit and acceptance.</p>
  </section>;
}
