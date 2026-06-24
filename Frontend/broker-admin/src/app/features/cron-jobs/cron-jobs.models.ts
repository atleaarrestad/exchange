export interface CronJobExecutionRecord {
  readonly id: string;
  readonly scheduledAtUtc: string;
  readonly startedAtUtc: string;
  readonly completedAtUtc: string | null;
  readonly status: string;
  readonly resultMessage: string | null;
  readonly error: string | null;
  readonly runnerId: string;
}

export interface CronJobSummary {
  readonly jobName: string;
  readonly displayName?: string | null;
  readonly jobType: string;
  readonly isEnabled: boolean;
  readonly cronExpression: string;
  readonly timeoutSeconds: number;
  readonly nextRunAtUtc: string;
  readonly lastStartedAtUtc: string | null;
  readonly lastCompletedAtUtc: string | null;
  readonly lastRunStatus: string | null;
  readonly lastError: string | null;
}

export interface CronJobManualRunResult {
  readonly jobName: string;
  readonly scheduledAtUtc: string;
  readonly startedAtUtc: string;
  readonly completedAtUtc: string;
  readonly status: string;
  readonly resultMessage: string;
  readonly error: string | null;
}
