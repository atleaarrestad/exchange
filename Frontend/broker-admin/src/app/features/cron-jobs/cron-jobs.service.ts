import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AdminApiClientService } from '../../core/api/admin-api-client.service';
import { CronJobExecutionRecord, CronJobManualRunResult, CronJobSummary } from './cron-jobs.models';

const CRON_JOBS_ENDPOINT = '/api/admin/cron-jobs';

@Injectable({ providedIn: 'root' })
export class CronJobsService {
  private readonly apiClient = inject(AdminApiClientService);

  list(): Promise<readonly CronJobSummary[]> {
    return firstValueFrom(this.apiClient.get<unknown>(CRON_JOBS_ENDPOINT)).then((response) =>
      ensureCronJobsResponse(response, CRON_JOBS_ENDPOINT)
    );
  }

  runNow(jobName: string): Promise<CronJobManualRunResult> {
    const encodedJobName = encodeURIComponent(jobName);
    return firstValueFrom(
      this.apiClient.post<CronJobManualRunResult, Record<string, never>>(
        `${CRON_JOBS_ENDPOINT}/${encodedJobName}/run`,
        {}
      )
    );
  }

  getExecutions(jobName: string): Promise<readonly CronJobExecutionRecord[]> {
    const encodedJobName = encodeURIComponent(jobName);
    const endpoint = `${CRON_JOBS_ENDPOINT}/${encodedJobName}/executions`;
    return firstValueFrom(this.apiClient.get<unknown>(endpoint)).then((response) =>
      ensureExecutionRecordsResponse(response, endpoint)
    );
  }
}

function ensureCronJobsResponse(response: unknown, endpoint: string): readonly CronJobSummary[] {
  if (!Array.isArray(response)) {
    throw new Error(`Unexpected response from ${endpoint}: expected an array.`);
  }

  const jobs = response.filter(isCronJobSummary);
  if (jobs.length !== response.length) {
    throw new Error(`Unexpected response from ${endpoint}: received invalid cron job entries.`);
  }

  return jobs;
}

function isCronJobSummary(value: unknown): value is CronJobSummary {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const candidate = value as Partial<CronJobSummary>;
  return (
    typeof candidate.jobName === 'string' &&
    (typeof candidate.displayName === 'string' || candidate.displayName === null || candidate.displayName === undefined) &&
    typeof candidate.jobType === 'string' &&
    typeof candidate.isEnabled === 'boolean' &&
    typeof candidate.cronExpression === 'string' &&
    typeof candidate.timeoutSeconds === 'number' &&
    typeof candidate.nextRunAtUtc === 'string'
  );
}

function ensureExecutionRecordsResponse(
  response: unknown,
  endpoint: string
): readonly CronJobExecutionRecord[] {
  if (!Array.isArray(response)) {
    throw new Error(`Unexpected response from ${endpoint}: expected an array.`);
  }

  const executions = response.filter(isExecutionRecord);
  if (executions.length !== response.length) {
    throw new Error(`Unexpected response from ${endpoint}: received invalid execution entries.`);
  }

  return executions;
}

function isExecutionRecord(value: unknown): value is CronJobExecutionRecord {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const candidate = value as Partial<CronJobExecutionRecord>;
  return (
    typeof candidate.id === 'string' &&
    typeof candidate.scheduledAtUtc === 'string' &&
    typeof candidate.startedAtUtc === 'string' &&
    (typeof candidate.completedAtUtc === 'string' || candidate.completedAtUtc === null) &&
    typeof candidate.status === 'string' &&
    (typeof candidate.resultMessage === 'string' || candidate.resultMessage === null) &&
    (typeof candidate.error === 'string' || candidate.error === null) &&
    typeof candidate.runnerId === 'string'
  );
}
