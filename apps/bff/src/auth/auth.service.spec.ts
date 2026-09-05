import { UnauthorizedException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { JwtService } from '@nestjs/jwt';
import type { Pool } from 'pg';
import { AuthService } from './auth.service';
import { TokenService } from './token.service';

function makePool(queryImpl: (sql: string, params: unknown[]) => Promise<{ rows: unknown[] }>): Pool {
  return { query: jest.fn(queryImpl) } as unknown as Pool;
}

function makeTokens(): TokenService {
  const config = new ConfigService({ JWT_SECRET: 'test-secret', JWT_ACCESS_TTL: '900', JWT_REFRESH_TTL: '2592000' });
  return new TokenService(new JwtService({}), config);
}

describe('AuthService', () => {
  it('rejects unknown user with 401', async () => {
    const pool = makePool(async () => ({ rows: [] }));
    const service = new AuthService(pool, makeTokens());

    await expect(service.validateCredentials('nobody@example.com', 'x')).rejects.toBeInstanceOf(
      UnauthorizedException,
    );
  });

  it('issues a token pair and resolves the sole tenant on login', async () => {
    const { hash } = await import('@node-rs/argon2');
    const passwordHash = await hash('correct-horse');

    const pool = makePool(async (sql: string) => {
      if (sql.includes('FROM catalog.users')) {
        return { rows: [{ id: 'u1', email: 'ana@paroquia.org', password_hash: passwordHash, display_name: 'Ana' }] };
      }
      if (sql.includes('FROM catalog.memberships')) {
        return { rows: [{ tenantId: 't1', slug: 'diocese-sp', name: 'Diocese SP', role: 'admin' }] };
      }
      return { rows: [] };
    });

    const service = new AuthService(pool, makeTokens());
    const result = await service.login('ana@paroquia.org', 'correct-horse');

    expect(result.accessToken).toBeTruthy();
    expect(result.refreshToken).toBeTruthy();
    expect(result.activeTenant).toBe('diocese-sp');
    expect(result.tenants).toHaveLength(1);
  });
});
