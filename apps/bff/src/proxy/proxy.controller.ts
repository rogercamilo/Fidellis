import { All, Controller, Req, Res } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import type { Request, Response } from 'express';

/**
 * Encaminha `/api/*` para o core .NET, repassando o header Authorization — é ele que
 * carrega o claim de tenant que o core usa para resolver o schema. Em produção, BFF↔core
 * ficam na rede privada.
 */
@Controller('api')
export class ProxyController {
  constructor(private readonly config: ConfigService) {}

  @All('*')
  async forward(@Req() req: Request, @Res() res: Response): Promise<void> {
    const coreUrl = this.config.get<string>('CORE_URL', 'http://localhost:5080');
    const target = `${coreUrl}${req.originalUrl}`;

    const headers: Record<string, string> = { accept: 'application/json' };
    if (req.headers.authorization) headers.authorization = req.headers.authorization;

    const hasBody = !['GET', 'HEAD'].includes(req.method);
    if (hasBody) headers['content-type'] = 'application/json';

    try {
      const upstream = await fetch(target, {
        method: req.method,
        headers,
        body: hasBody ? JSON.stringify(req.body ?? {}) : undefined,
      });

      const text = await upstream.text();
      res.status(upstream.status);
      const contentType = upstream.headers.get('content-type');
      if (contentType) res.setHeader('content-type', contentType);
      res.send(text);
    } catch {
      res.status(502).json({ error: 'Core indisponível.', target });
    }
  }
}
