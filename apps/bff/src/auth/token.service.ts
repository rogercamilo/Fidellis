import { Injectable } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { JwtService } from '@nestjs/jwt';

export interface AccessClaims {
  sub: string;
  email: string;
  /** Slug do tenant assumido; lido pelo core para resolver o schema t_<slug>. */
  tenant?: string;
  /** Papel do usuário no tenant ativo; lido pelo core para o RBAC (ex.: admin, treasurer). */
  role?: string;
}

/** Emite/valida JWT HS256 com o segredo compartilhado com o core .NET. */
@Injectable()
export class TokenService {
  private readonly secret: string;
  private readonly accessTtl: number;
  private readonly refreshTtl: number;

  constructor(
    private readonly jwt: JwtService,
    config: ConfigService,
  ) {
    this.secret = config.get<string>('JWT_SECRET', 'change-me-in-prod-please-use-a-long-random-secret');
    this.accessTtl = Number(config.get<string>('JWT_ACCESS_TTL', '900'));
    this.refreshTtl = Number(config.get<string>('JWT_REFRESH_TTL', '2592000'));
  }

  signAccess(claims: AccessClaims): string {
    return this.jwt.sign(claims, { secret: this.secret, expiresIn: this.accessTtl });
  }

  signRefresh(sub: string): string {
    return this.jwt.sign({ sub, typ: 'refresh' }, { secret: this.secret, expiresIn: this.refreshTtl });
  }

  verify<T extends object = Record<string, unknown>>(token: string): T {
    return this.jwt.verify<T>(token, { secret: this.secret });
  }
}
