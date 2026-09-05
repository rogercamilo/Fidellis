import { ConflictException, Inject, Injectable, InternalServerErrorException, Logger } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { hash as argonHash } from '@node-rs/argon2';
import { randomUUID } from 'node:crypto';
import { Pool } from 'pg';
import { PG_POOL } from '../database/database.module';
import { TokenService } from '../auth/token.service';

export interface SignupInput {
  slug: string;
  tenantName: string;
  email: string;
  password: string;
  displayName?: string;
  organizationName?: string;
}

/**
 * Onboarding de uma instituição: cria o primeiro usuário (hash Argon2, no BFF) e delega ao core a
 * criação do tenant + schema + associação (membership) + organização-raiz, já vinculando o admin a
 * ela. Faz auto-login ao final. Evita depender de seed manual.
 */
@Injectable()
export class OnboardingService {
  private readonly logger = new Logger(OnboardingService.name);

  constructor(
    @Inject(PG_POOL) private readonly pool: Pool,
    private readonly tokens: TokenService,
    private readonly config: ConfigService,
  ) {}

  async signup(input: SignupInput) {
    const slug = input.slug.trim().toLowerCase();
    const email = input.email.trim().toLowerCase();

    const dup = await this.pool.query('SELECT 1 FROM catalog.users WHERE email = $1', [email]);
    if (dup.rowCount) throw new ConflictException('E-mail já cadastrado.');

    const userId = randomUUID();
    const passwordHash = await argonHash(input.password);
    await this.pool.query(
      'INSERT INTO catalog.users (id, email, password_hash, display_name) VALUES ($1, $2, $3, $4)',
      [userId, email, passwordHash, input.displayName ?? null],
    );

    const coreUrl = this.config.get<string>('CORE_URL', 'http://localhost:5080');
    let tenant: unknown;
    try {
      const res = await fetch(`${coreUrl}/api/tenants`, {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({
          slug,
          name: input.tenantName,
          adminUserId: userId,
          organizationName: input.organizationName,
        }),
      });
      if (res.status === 409) {
        await this.cleanup(userId);
        throw new ConflictException(`Tenant '${slug}' já existe.`);
      }
      if (!res.ok) {
        throw new Error(`core respondeu ${res.status}: ${await res.text()}`);
      }
      tenant = await res.json();
    } catch (err) {
      if (err instanceof ConflictException) throw err;
      this.logger.error(`Falha ao provisionar tenant: ${String(err)}`);
      await this.cleanup(userId);
      throw new InternalServerErrorException('Falha ao provisionar o tenant.');
    }

    return {
      user: { id: userId, email, displayName: input.displayName ?? null },
      tenant,
      accessToken: this.tokens.signAccess({ sub: userId, email, tenant: slug }),
      refreshToken: this.tokens.signRefresh(userId),
      activeTenant: slug,
    };
  }

  private async cleanup(userId: string): Promise<void> {
    await this.pool.query('DELETE FROM catalog.users WHERE id = $1', [userId]).catch(() => undefined);
  }
}
