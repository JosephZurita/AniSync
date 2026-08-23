import { Link } from "react-router-dom";
import { ArrowRight, CircleCheck, CircleX, ListChecks, RefreshCw } from "lucide-react";
import { useBulkSyncStatus } from "@/api/queries";
import { Button } from "@/components/ui/button";
import {
    Card,
    CardAction,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle
} from "@/components/ui/card";

export const BulkSyncCard = () => {
    const { data: status, isLoading, isError } = useBulkSyncStatus(true);
    const isRunning = status?.state === "running";
    const progress = status?.totalSeries
        ? Math.min(100, Math.round((status.processedSeries / status.totalSeries) * 100))
        : 0;

    return (
        <Card>
            <CardHeader>
                <CardTitle>Sync Library</CardTitle>
                <CardDescription>
                    Review your watched Shoko series and choose exactly what to sync.
                </CardDescription>
                <CardAction>
                    <Button asChild>
                        <Link to="/sync">
                            {isRunning ? <RefreshCw className="animate-spin" /> : <ListChecks />}
                            {isRunning ? "View progress" : "Review library"}
                            <ArrowRight />
                        </Link>
                    </Button>
                </CardAction>
            </CardHeader>
            <CardContent>
                {isLoading && <p className="text-sm text-muted-foreground">Loading sync status…</p>}
                {isError && (
                    <p className="flex items-center gap-2 text-sm text-destructive">
                        <CircleX className="size-4" /> Couldn't load bulk sync status.
                    </p>
                )}
                {status?.state === "idle" && (
                    <p className="text-sm text-muted-foreground">No bulk sync has been run yet.</p>
                )}
                {isRunning && (
                    <div className="space-y-2">
                        <div className="flex justify-between gap-4 text-sm">
                            <span className="truncate text-muted-foreground">
                                {status.currentSeries ?? "Preparing selection…"}
                            </span>
                            <span className="shrink-0 font-medium">
                                {status.processedSeries}/{status.totalSeries}
                            </span>
                        </div>
                        <div className="h-2 overflow-hidden rounded-full bg-muted">
                            <div
                                className="h-full rounded-full bg-primary transition-[width] duration-300"
                                style={{ width: `${progress}%` }}
                            />
                        </div>
                    </div>
                )}
                {status?.state === "completed" && (
                    <p className="flex items-center gap-2 text-sm">
                        <CircleCheck className="size-4 text-success" />
                        Last run processed {status.processedSeries} selected series;{" "}
                        {status.updatedSeries} required updates.
                    </p>
                )}
                {(status?.state === "failed" || status?.state === "cancelled") && (
                    <p className="flex items-center gap-2 text-sm text-destructive">
                        <CircleX className="size-4" />
                        {status.error ?? "The last bulk sync did not finish."}
                    </p>
                )}
            </CardContent>
        </Card>
    );
};
