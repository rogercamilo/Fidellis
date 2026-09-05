import { Injectable, UnauthorizedException } from '@nestjs/common';
import { AuthService, TenantMembership } from '../auth/auth.service';
import { TokenService } from '../auth/token.service';

/** Resolução/seleção do tenant ativo da sessão. */
@Injectable()
export class TenantService {
  constructor(
    private readonly auth: AuthService,
    private readonly tokens: TokenService,
  ) {}

  async listForToken(accessToken: string): Promise<TenantMembership[]> {
    const { sub } = this.verify(accessToken);
    return this.auth.getMemberships(sub);
  }

  /** Emite novo access token assumindo o tenant escolhido (valida a associação). */
  async select(accessToken: string, slug: string): Promise<{ accessToken: string; activeTenant: string }> {
    const { sub, email } = this.verify(accessToken);
    const memberships = await this.auth.getMemberships(sub);
    const match = memberships.find((t) => t.slug === slug.trim().toLowerCase());
    if (!match) throw new UnauthorizedException(`Usuário não pertence ao tenant '${slug}'.`);

    return {
      accessToken: this.tokens.signAccess({ sub, email, tenant: match.slug }),
      activeTenant: match.slug,
    };
  }

  private verify(accessToken: string): { sub: string; email: string } {
    try {
      const claims = this.tokens.verify<{ sub: string; email: string }>(accessToken);
      return { sub: claims.sub, email: claims.email };
    } catch {
      throw new UnauthorizedException('Token inválido ou expirado.');
    }
  }
}
