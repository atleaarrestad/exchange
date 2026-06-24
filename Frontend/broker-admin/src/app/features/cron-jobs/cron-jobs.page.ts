import { DatePipe } from '@angular/common';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatTabChangeEvent, MatTabsModule } from '@angular/material/tabs';
import { RouterLink } from '@angular/router';
import { CronJobSummary } from './cron-jobs.models';
import { CronJobsService } from './cron-jobs.service';

const ALL_JOB_TYPES_TAB = 'all';
const UNTYPED_JOB_TYPE = 'untyped';

interface CronJobTypeTab {
  readonly key: string;
  readonly label: string;
  readonly count: number;
}

@Component({
  selector: 'app-cron-jobs-page',
  standalone: true,
  imports: [DatePipe, MatButtonModule, MatCardModule, MatChipsModule, MatTabsModule, RouterLink],
  templateUrl: './cron-jobs.page.html',
  styleUrl: './cron-jobs.page.scss'
})
export class CronJobsPageComponent implements OnInit, OnDestroy {
  private readonly cronJobsService = inject(CronJobsService);
  private readonly runningJobs = new Set<string>();
  private readonly nowTickHandle = window.setInterval(() => {
    this.nowEpochMs = Date.now();
  }, 30_000);

  protected jobs: readonly CronJobSummary[] = [];
  protected displayedJobs: readonly CronJobSummary[] = [];
  protected jobTypeTabs: readonly CronJobTypeTab[] = [{ key: ALL_JOB_TYPES_TAB, label: 'All', count: 0 }];
  protected selectedTabIndex = 0;
  protected nowEpochMs = Date.now();
  protected isLoading = false;
  protected errorMessage: string | null = null;
  protected successMessage: string | null = null;

  async ngOnInit(): Promise<void> {
    await this.refresh();
  }

  ngOnDestroy(): void {
    window.clearInterval(this.nowTickHandle);
  }

  protected isRunInProgress(jobName: string): boolean {
    return this.runningJobs.has(jobName);
  }

  protected displayNameFor(job: CronJobSummary): string {
    const displayName = job.displayName?.trim();
    if (displayName !== undefined && displayName !== null && displayName.length > 0) {
      return displayName;
    }

    return humanizeName(job.jobName);
  }

  protected statusPillClass(status: string | null): string {
    switch ((status ?? '').toLowerCase()) {
      case 'succeeded':
        return 'status-pill status-pill--success';
      case 'failed':
      case 'timedout':
        return 'status-pill status-pill--error';
      case 'running':
        return 'status-pill status-pill--running';
      default:
        return 'status-pill';
    }
  }

  protected nextRunRelative(nextRunAtUtc: string): string {
    const nextRunEpochMs = Date.parse(nextRunAtUtc);
    if (Number.isNaN(nextRunEpochMs)) {
      return 'unknown';
    }

    const deltaMs = nextRunEpochMs - this.nowEpochMs;
    if (Math.abs(deltaMs) < 30_000) {
      return 'now';
    }

    if (deltaMs < 0) {
      return `${formatDuration(Math.abs(deltaMs))} overdue`;
    }

    return `in ${formatDuration(deltaMs)}`;
  }

  protected lastRunSummary(job: CronJobSummary): string {
    const status = job.lastRunStatus ?? 'No run';
    const runAtUtc = job.lastCompletedAtUtc ?? job.lastStartedAtUtc;
    if (runAtUtc === null) {
      return status;
    }

    const runEpochMs = Date.parse(runAtUtc);
    if (Number.isNaN(runEpochMs)) {
      return status;
    }

    const elapsedMs = this.nowEpochMs - runEpochMs;
    if (elapsedMs < 0) {
      return `${status} \u00b7 just now`;
    }

    if (elapsedMs < 30_000) {
      return `${status} \u00b7 just now`;
    }

    return `${status} \u00b7 ${formatDuration(elapsedMs)} ago`;
  }

  protected async refresh(): Promise<void> {
    this.isLoading = true;
    this.errorMessage = null;

    try {
      this.jobs = toUniqueCronJobs(await this.cronJobsService.list());
      this.jobTypeTabs = buildJobTypeTabs(this.jobs);
      this.selectedTabIndex = getSelectedTabIndex(this.jobTypeTabs, this.selectedTabIndex);
      this.displayedJobs = filterJobsForTab(this.jobs, this.jobTypeTabs[this.selectedTabIndex]?.key ?? ALL_JOB_TYPES_TAB);
    } catch (error) {
      this.errorMessage = getErrorMessage(error);
    } finally {
      this.isLoading = false;
    }
  }

  protected onTabChanged(event: MatTabChangeEvent): void {
    this.selectedTabIndex = event.index;
    this.displayedJobs = filterJobsForTab(
      this.jobs,
      this.jobTypeTabs[this.selectedTabIndex]?.key ?? ALL_JOB_TYPES_TAB
    );
  }

  protected tabLabel(tab: CronJobTypeTab): string {
    return `${tab.label} (${tab.count})`;
  }

  protected async runNow(jobName: string): Promise<void> {
    this.runningJobs.add(jobName);
    this.errorMessage = null;
    this.successMessage = null;

    try {
      const run = await this.cronJobsService.runNow(jobName);
      await this.refresh();
      this.successMessage = `Ran "${run.jobName}" with status ${run.status}.`;
    } catch (error) {
      this.errorMessage = getErrorMessage(error);
    } finally {
      this.runningJobs.delete(jobName);
    }
  }
}

function humanizeName(value: string): string {
  const words = value
    .split(/[-_]+/u)
    .map((word) => word.trim())
    .filter((word) => word.length > 0)
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1));

  if (words.length === 0) {
    return value;
  }

  return words.join(' ');
}

function formatDuration(durationMs: number): string {
  const totalMinutes = Math.floor(durationMs / 60_000);
  const totalHours = Math.floor(totalMinutes / 60);
  const days = Math.floor(totalHours / 24);
  const hours = totalHours % 24;
  const minutes = totalMinutes % 60;

  if (days > 0) {
    return hours > 0 ? `${days}d ${hours}h` : `${days}d`;
  }

  if (hours > 0) {
    return minutes > 0 ? `${hours}h ${minutes}m` : `${hours}h`;
  }

  return `${Math.max(minutes, 1)}m`;
}

function getErrorMessage(error: unknown): string {
  if (error instanceof Error && error.message.trim().length > 0) {
    return error.message;
  }

  return 'Request failed.';
}

function toUniqueCronJobs(jobs: readonly CronJobSummary[]): readonly CronJobSummary[] {
  const byName = new Map<string, CronJobSummary>();
  for (const job of jobs) {
    if (!byName.has(job.jobName)) {
      byName.set(job.jobName, { ...job, jobType: normalizeJobType(job.jobType) });
    }
  }

  return Array.from(byName.values());
}

function normalizeJobType(jobType: string): string {
  const normalized = jobType.trim().toLowerCase();
  return normalized.length === 0 ? UNTYPED_JOB_TYPE : normalized;
}

function buildJobTypeTabs(jobs: readonly CronJobSummary[]): readonly CronJobTypeTab[] {
  const countsByType = new Map<string, number>();
  for (const job of jobs) {
    const nextCount = (countsByType.get(job.jobType) ?? 0) + 1;
    countsByType.set(job.jobType, nextCount);
  }

  const typedTabs = Array.from(countsByType.entries())
    .sort(([leftType], [rightType]) => leftType.localeCompare(rightType))
    .map(([type, count]) => ({
      key: type,
      label: toJobTypeLabel(type),
      count
    }));

  return [{ key: ALL_JOB_TYPES_TAB, label: 'All', count: jobs.length }, ...typedTabs];
}

function getSelectedTabIndex(tabs: readonly CronJobTypeTab[], currentIndex: number): number {
  if (tabs.length === 0) {
    return 0;
  }

  if (currentIndex < 0 || currentIndex >= tabs.length) {
    return 0;
  }

  return currentIndex;
}

function filterJobsForTab(jobs: readonly CronJobSummary[], tabKey: string): readonly CronJobSummary[] {
  if (tabKey === ALL_JOB_TYPES_TAB) {
    return jobs;
  }

  return jobs.filter((job) => job.jobType === tabKey);
}

function toJobTypeLabel(type: string): string {
  if (type === UNTYPED_JOB_TYPE) {
    return 'Untyped';
  }

  return type.charAt(0).toUpperCase() + type.slice(1);
}
