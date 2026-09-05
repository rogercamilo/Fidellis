import { Controller, Get, Inject } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { Pool } from 'pg';
import { PG_POOL } from '../database/database.module';

@Controller('health')
export class HealthController {
  constructor(
    @Inject(PG_POOL) private readonly pool: Pool,
    private readonly config: ConfigService,
  ) {}

  /** Liveness — o processo está de pé. */
  @Get()
  live() {
    return { status: 'live', service: 'bff' };
  }

  /** Readiness — Postgres (catalog) e o core respondem. */
  @Get('ready')
  async ready() {
    const checks: Record<string, string> = {};
    let healthy = true;

    try {
      await this.pool.query('SELECT 1');
      checks.postgres = 'ok';
    } catch {
      checks.postgres = 'down';
      healthy = false;
    }

    const coreUrl = this.config.get<string>('CORE_URL', 'http://localhost:5080');
    try {
      const res = await fetch(`${coreUrl}/health/live`);
      checks.core = res.ok ? 'ok' : 'down';
      if (!res.ok) healthy = false;
    } catch {
      checks.core = 'down';
      healthy = false;
    }

    return { status: healthy ? 'ready' : 'unready', checks };
  }
}
