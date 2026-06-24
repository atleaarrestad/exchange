import { DatePipe } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CronJobExecutionRecord } from './cron-jobs.models';
import { CronJobsService } from './cron-jobs.service';

@Component({
  selector: 'app-cron-job-logs-page',
  standalone: true,
  imports: [DatePipe, MatButtonModule, MatCardModule, RouterLink],
  templateUrl: './cron-job-logs.page.html',
  styleUrl: './cron-job-logs.page.scss'
})
export class CronJobLogsPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly cronJobsService = inject(CronJobsService);

  protected jobName = '';
  protected logs: readonly CronJobExecutionRecord[] = [];
  protected isLoading = false;
  protected errorMessage: string | null = null;

  async ngOnInit(): Promise<void> {
    const routeJobName = this.route.snapshot.paramMap.get('jobName');
    if (routeJobName === null || routeJobName.trim().length === 0) {
      this.errorMessage = 'Invalid cron job name.';
      return;
    }

    this.jobName = routeJobName;
    await this.refresh();
  }

  protected statusClass(status: string): string {
    switch (status.toLowerCase()) {
      case 'succeeded':
        return 'status status--success';
      case 'failed':
      case 'timedout':
        return 'status status--error';
      default:
        return 'status';
    }
  }

  protected async refresh(): Promise<void> {
    this.isLoading = true;
    this.errorMessage = null;

    try {
      this.logs = await this.cronJobsService.getExecutions(this.jobName);
    } catch (error) {
      this.errorMessage = getErrorMessage(error);
    } finally {
      this.isLoading = false;
    }
  }
}

function getErrorMessage(error: unknown): string {
  if (error instanceof Error && error.message.trim().length > 0) {
    return error.message;
  }

  return 'Request failed.';
}
