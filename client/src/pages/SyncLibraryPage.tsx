import { useEffect, useMemo, useRef, useState } from "react";
import { CircleCheck, CircleX, ListChecks, RefreshCw, Search } from "lucide-react";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
    useBulkSyncPreview,
    useBulkSyncStatus,
    useDashboard,
    useStartBulkSync
} from "@/api/queries";
import { useAuthStore } from "@/store/auth";
import {
    AlertDialog,
    AlertDialogAction,
    AlertDialogCancel,
    AlertDialogContent,
    AlertDialogDescription,
    AlertDialogFooter,
    AlertDialogHeader,
    AlertDialogTitle,
    AlertDialogTrigger
} from "@/components/ui/alert-dialog";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import {
    Table,
    TableBody,
    TableCell,
    TableHead,
    TableHeader,
    TableRow
} from "@/components/ui/table";

const SyncLibraryPage = () => {
    const queryClient = useQueryClient();
    const apiKey = useAuthStore((state) => state.apiKey);
    const { data: dashboard } = useDashboard();
    const { data: status, isLoading: statusLoading } = useBulkSyncStatus(true);
    const isRunning = status?.state === "running";
    const {
        data: preview,
        isLoading: previewLoading,
        isError: previewError,
        refetch: refetchPreview
    } = useBulkSyncPreview(!isRunning);
    const startSync = useStartBulkSync();
    const [search, setSearch] = useState("");
    const [selectedIDs, setSelectedIDs] = useState<Set<number>>(new Set());
    const selectionInitialized = useRef(false);
    const knownPreviewIDs = useRef<Set<number>>(new Set());
    const previousState = useRef(status?.state);

    useEffect(() => {
        if (!preview) return;

        const nextIDs = new Set(preview.items.map((item) => item.seriesID));
        const previousIDs = knownPreviewIDs.current;
        setSelectedIDs((current) => {
            if (!selectionInitialized.current) return new Set(nextIDs);

            const reconciled = new Set(
                Array.from(current).filter((seriesID) => nextIDs.has(seriesID))
            );
            for (const seriesID of nextIDs) {
                if (!previousIDs.has(seriesID)) reconciled.add(seriesID);
            }
            return reconciled;
        });
        knownPreviewIDs.current = nextIDs;
        selectionInitialized.current = true;
    }, [preview]);

    useEffect(() => {
        if (previousState.current === "running" && status?.state === "completed") {
            toast.success(
                `Bulk sync finished: ${status.updatedSeries} series updated${
                    status.failedSeries ? `, ${status.failedSeries} failed` : ""
                }`
            );
            queryClient.invalidateQueries({ queryKey: ["dashboard"] });
            queryClient.invalidateQueries({ queryKey: ["history"] });
        } else if (previousState.current === "running" && status?.state === "failed") {
            toast.error(status.error ?? "Bulk sync failed");
        }
        previousState.current = status?.state;
    }, [queryClient, status]);

    const filteredItems = useMemo(() => {
        const query = search.trim().toLocaleLowerCase();
        if (!query) return preview?.items ?? [];
        return (preview?.items ?? []).filter(
            (item) =>
                item.title.toLocaleLowerCase().includes(query) ||
                item.anidbAnimeID.toString().includes(query)
        );
    }, [preview, search]);

    const allVisibleSelected =
        filteredItems.length > 0 &&
        filteredItems.every((item) => selectedIDs.has(item.seriesID));
    const progress = status?.totalSeries
        ? Math.min(100, Math.round((status.processedSeries / status.totalSeries) * 100))
        : 0;
    const providerNames = dashboard
        ? Object.entries(dashboard.providers)
              .filter(([, provider]) => provider.connected)
              .map(([name]) => (name === "aniList" ? "AniList" : "MyAnimeList"))
        : [];

    const toggleSeries = (seriesID: number) => {
        setSelectedIDs((current) => {
            const next = new Set(current);
            if (next.has(seriesID)) next.delete(seriesID);
            else next.add(seriesID);
            return next;
        });
    };

    const toggleVisible = () => {
        setSelectedIDs((current) => {
            const next = new Set(current);
            for (const item of filteredItems) {
                if (allVisibleSelected) next.delete(item.seriesID);
                else next.add(item.seriesID);
            }
            return next;
        });
    };

    const refreshPreview = async () => {
        const result = await refetchPreview();
        if (result.data) {
            toast.success("Provider comparison refreshed");
        }
    };

    const imageUrl = (path: string | null) =>
        path
            ? `${path}${path.includes("?") ? "&" : "?"}apikey=${encodeURIComponent(apiKey)}`
            : null;

    return (
        <div className="space-y-6">
            <div>
                <h1 className="text-2xl font-semibold tracking-tight">Sync Library</h1>
                <p className="mt-1 text-sm text-muted-foreground">
                    Review only the Shoko series whose watched progress still needs to be
                    added or updated on {providerNames.join(" and ") || "your connected providers"}.
                </p>
            </div>

            {!statusLoading && dashboard && !dashboard.isAuthenticated && (
                <Alert variant="destructive">
                    <CircleX />
                    <AlertDescription>
                        Connect at least one provider on the dashboard before starting a sync.
                    </AlertDescription>
                </Alert>
            )}

            {status && status.state !== "idle" && (
                <Card>
                    <CardHeader>
                        <CardTitle>{isRunning ? "Sync in progress" : "Last sync"}</CardTitle>
                        <CardDescription>
                            {isRunning
                                ? status.currentSeries ?? "Preparing selected series…"
                                : `${status.processedSeries} selected series processed.`}
                        </CardDescription>
                    </CardHeader>
                    <CardContent className="space-y-3">
                        {isRunning && (
                            <>
                                <div className="flex justify-between text-sm">
                                    <span>{status.updatedSeries} updated</span>
                                    <span className="font-medium">
                                        {status.processedSeries}/{status.totalSeries}
                                    </span>
                                </div>
                                <div
                                    className="h-2 overflow-hidden rounded-full bg-muted"
                                    role="progressbar"
                                    aria-label="Bulk sync progress"
                                    aria-valuemin={0}
                                    aria-valuemax={status.totalSeries}
                                    aria-valuenow={status.processedSeries}
                                >
                                    <div
                                        className="h-full rounded-full bg-primary transition-[width] duration-300"
                                        style={{ width: `${progress}%` }}
                                    />
                                </div>
                            </>
                        )}
                        {status.state === "completed" && (
                            <p className="flex items-center gap-2 text-sm">
                                <CircleCheck className="size-4 text-success" />
                                {status.updatedSeries} required updates
                                {status.failedSeries > 0 && `; ${status.failedSeries} failed`}.
                            </p>
                        )}
                        {(status.state === "failed" || status.state === "cancelled") && (
                            <p className="flex items-center gap-2 text-sm text-destructive">
                                <CircleX className="size-4" />
                                {status.error ?? "The sync did not finish."}
                            </p>
                        )}
                    </CardContent>
                </Card>
            )}

            <Card>
                <CardHeader>
                    <CardTitle>Review series</CardTitle>
                    <CardDescription>
                        Refresh compares current Shoko progress with every connected account.
                        Existing provider progress is never moved backwards, and bulk runs do not
                        infer rewatches.
                    </CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                    <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
                        <div className="relative flex-1">
                            <Search className="absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
                            <Input
                                value={search}
                                onChange={(event) => setSearch(event.target.value)}
                                placeholder="Search title or AniDB ID"
                                className="pl-8"
                            />
                        </div>
                        <div className="flex items-center gap-2">
                            <Button
                                type="button"
                                variant="outline"
                                onClick={toggleVisible}
                                disabled={filteredItems.length === 0 || isRunning}
                            >
                                {allVisibleSelected ? "Clear visible" : "Select visible"}
                            </Button>
                            <Button
                                type="button"
                                variant="ghost"
                                onClick={() => void refreshPreview()}
                                disabled={previewLoading || isRunning}
                            >
                                <RefreshCw className={previewLoading ? "animate-spin" : ""} />
                                Refresh
                            </Button>
                        </div>
                    </div>

                    <div className="flex items-center justify-between text-sm">
                        <span className="text-muted-foreground">
                            {filteredItems.length} of {preview?.items.length ?? 0} series shown
                        </span>
                        <span className="font-medium">{selectedIDs.size} selected</span>
                    </div>

                    {previewLoading && <Skeleton className="h-72 w-full" />}
                    {previewError && (
                        <Alert variant="destructive">
                            <CircleX />
                            <AlertDescription>Couldn't load watched series from Shoko.</AlertDescription>
                        </Alert>
                    )}
                    {!previewLoading && !previewError && preview?.items.length === 0 && (
                        <div className="py-12 text-center text-muted-foreground">
                            <CircleCheck className="mx-auto mb-2 size-7 text-success" />
                            <p className="text-sm">Your connected anime lists are up to date.</p>
                        </div>
                    )}
                    {!previewLoading && !previewError && filteredItems.length > 0 && (
                        <div className="overflow-hidden rounded-lg border">
                            <Table>
                                <TableHeader>
                                    <TableRow>
                                        <TableHead className="w-10">
                                            <input
                                                type="checkbox"
                                                checked={allVisibleSelected}
                                                onChange={toggleVisible}
                                                disabled={isRunning}
                                                aria-label="Select all visible series"
                                                className="size-4 accent-primary"
                                            />
                                        </TableHead>
                                        <TableHead>Series</TableHead>
                                        <TableHead className="text-right">Shoko progress</TableHead>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {filteredItems.map((item) => {
                                        const poster = imageUrl(item.image);
                                        return (
                                            <TableRow
                                                key={item.seriesID}
                                                data-state={
                                                    selectedIDs.has(item.seriesID)
                                                        ? "selected"
                                                        : undefined
                                                }
                                            >
                                                <TableCell>
                                                    <input
                                                        type="checkbox"
                                                        checked={selectedIDs.has(item.seriesID)}
                                                        onChange={() => toggleSeries(item.seriesID)}
                                                        disabled={isRunning}
                                                        aria-label={`Sync ${item.title}`}
                                                        className="size-4 accent-primary"
                                                    />
                                                </TableCell>
                                                <TableCell>
                                                    <div className="flex min-w-0 items-center gap-3">
                                                        {poster ? (
                                                            <img
                                                                src={poster}
                                                                alt=""
                                                                loading="lazy"
                                                                className="h-14 w-10 shrink-0 rounded object-cover"
                                                            />
                                                        ) : (
                                                            <div className="h-14 w-10 shrink-0 rounded bg-muted" />
                                                        )}
                                                        <div className="min-w-0">
                                                            <p className="max-w-[28rem] truncate font-medium">
                                                                {item.title}
                                                            </p>
                                                            <p className="text-xs text-muted-foreground">
                                                                AniDB {item.anidbAnimeID}
                                                            </p>
                                                        </div>
                                                    </div>
                                                </TableCell>
                                                <TableCell className="text-right font-medium">
                                                    Episode {item.episodeNumber}
                                                    {item.totalEpisodes > 0 &&
                                                        ` of ${item.totalEpisodes}`}
                                                </TableCell>
                                            </TableRow>
                                        );
                                    })}
                                </TableBody>
                            </Table>
                        </div>
                    )}

                    <div className="flex flex-col justify-between gap-3 border-t pt-4 sm:flex-row sm:items-center">
                        <p className="text-sm text-muted-foreground">
                            Sync delay and “Sync only on completion” settings are respected.
                        </p>
                        <AlertDialog>
                            <AlertDialogTrigger asChild>
                                <Button
                                    disabled={
                                        selectedIDs.size === 0 ||
                                        isRunning ||
                                        startSync.isPending ||
                                        dashboard?.isAuthenticated === false
                                    }
                                >
                                    {isRunning || startSync.isPending ? (
                                        <RefreshCw className="animate-spin" />
                                    ) : (
                                        <ListChecks />
                                    )}
                                    Sync {selectedIDs.size} selected
                                </Button>
                            </AlertDialogTrigger>
                            <AlertDialogContent>
                                <AlertDialogHeader>
                                    <AlertDialogTitle>
                                        Sync {selectedIDs.size} selected series?
                                    </AlertDialogTitle>
                                    <AlertDialogDescription>
                                        The reviewed Shoko progress will be pushed to{" "}
                                        {providerNames.join(" and ") || "all connected providers"}.
                                        Series already up to date will remain unchanged.
                                    </AlertDialogDescription>
                                </AlertDialogHeader>
                                <AlertDialogFooter>
                                    <AlertDialogCancel>Cancel</AlertDialogCancel>
                                    <AlertDialogAction
                                        onClick={() => startSync.mutate(Array.from(selectedIDs))}
                                    >
                                        Start sync
                                    </AlertDialogAction>
                                </AlertDialogFooter>
                            </AlertDialogContent>
                        </AlertDialog>
                    </div>
                </CardContent>
            </Card>
        </div>
    );
};

export default SyncLibraryPage;
