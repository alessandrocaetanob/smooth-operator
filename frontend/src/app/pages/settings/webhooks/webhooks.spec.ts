import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';

import { WebhooksSettings } from './webhooks';
import { Webhook, WebhooksService } from '../../../services/webhooks.service';
import { ConfirmDialogService } from '../../../shared/confirm-dialog/confirm-dialog.service';

const baseWebhook: Webhook = {
  id: 'w1',
  name: 'siem',
  url: 'https://example.com/hook',
  eventTypes: 'user.*,connection.*',
  enabled: true,
  createdAt: '2026-05-22T20:00:00Z',
  consecutiveFailures: 0,
};

describe('WebhooksSettings', () => {
  let component: WebhooksSettings;
  let fixture: ComponentFixture<WebhooksSettings>;
  let svc: {
    list: ReturnType<typeof signal<Webhook[]>>;
    load: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
    update: ReturnType<typeof vi.fn>;
    remove: ReturnType<typeof vi.fn>;
    rotateSecret: ReturnType<typeof vi.fn>;
    sendTest: ReturnType<typeof vi.fn>;
  };
  let confirm: { ask: ReturnType<typeof vi.fn> };

  beforeEach(async () => {
    svc = {
      list: signal<Webhook[]>([baseWebhook]),
      load: vi.fn(() => of([baseWebhook])),
      create: vi.fn(() => of({ id: 'w-new', name: 'fresh', secret: 'whsec_new' })),
      update: vi.fn(() => of(undefined)),
      remove: vi.fn(() => of(undefined)),
      rotateSecret: vi.fn(() => of({ id: 'w1', name: 'siem', secret: 'whsec_rot' })),
      sendTest: vi.fn(() => of(undefined)),
    };
    confirm = { ask: vi.fn(() => Promise.resolve(true)) };

    await TestBed.configureTestingModule({
      imports: [WebhooksSettings],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService(),
        { provide: WebhooksService, useValue: svc },
        { provide: ConfirmDialogService, useValue: confirm },
      ],
    }).compileComponents();

    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', {
      common: {
        actions: {
          delete: 'Delete',
        },
      },
      pages: {
        settingsWebhooks: {
          errors: {
            loadFailed: 'Failed to load webhooks.',
            nameUrlRequired: 'Name and URL are required.',
            saveFailed: 'Failed to save webhook.',
            createFailed: 'Failed to create webhook.',
            updateFailed: 'Failed to update webhook.',
            testFailed: 'Failed to queue test event.',
            deleteFailed: 'Failed to delete webhook.',
            rotateFailed: 'Failed to rotate secret.',
            copyFailed: 'Could not copy to clipboard.',
          },
          messages: {
            updated: 'Webhook endpoint updated.',
            created: 'Webhook endpoint created.',
            disabled: 'Webhook disabled.',
            enabled: 'Webhook enabled.',
            testQueued: 'Test event queued for "{{name}}". It will be delivered shortly.',
            deleted: 'Webhook endpoint deleted.',
            rotated: 'Signing secret rotated.',
            secretCopied: 'Signing secret copied to clipboard.',
            urlCopied: 'Endpoint URL copied to clipboard.',
          },
          confirmDelete: {
            title: 'Delete webhook endpoint?',
            message: '"{{name}}" will be removed and stop receiving events. This cannot be undone.',
          },
          confirmRotate: {
            title: 'Rotate signing secret?',
            message:
              'The current secret for "{{name}}" stops working immediately — update your receiver with the new secret right away.',
            confirmLabel: 'Rotate secret',
          },
          events: {
            all: 'All events',
            users: 'Users & authentication',
            connections: 'Connections',
            credentials: 'Credentials',
            sso: 'Single sign-on',
            groups: 'Groups',
            invitations: 'Invitations',
            webhooks: 'Webhooks',
            system: 'System',
          },
          status: {
            disabled: 'Disabled',
            noDeliveries: 'No deliveries yet',
            healthy: 'Healthy',
            failing: 'Failing ({{count}})',
          },
        },
      },
    });
    translate.use('en');

    fixture = TestBed.createComponent(WebhooksSettings);
    component = fixture.componentInstance;
  });

  it('creates', () => {
    expect(component).toBeTruthy();
  });

  describe('refresh / lifecycle', () => {
    it('calls load on init', () => {
      component.ngOnInit();
      expect(svc.load).toHaveBeenCalled();
    });

    it('surfaces a load error via the error signal', () => {
      svc.load.mockReturnValueOnce(throwError(() => ({ error: { message: 'boom' } })));
      component.refresh();
      expect(component.error()).toBe('boom');
    });
  });

  describe('event-subscription editor', () => {
    it('selectAllEvents resets selection to ["*"]', () => {
      component.selectedEvents.set(['user.*']);
      component.selectAllEvents();
      expect(component.selectedEvents()).toEqual(['*']);
      expect(component.allEventsSelected()).toBe(true);
    });

    it('toggleCategory adds a category and drops the "*" wildcard', () => {
      component.selectedEvents.set(['*']);
      component.toggleCategory('user.*');
      expect(component.selectedEvents()).toEqual(['user.*']);
      expect(component.isCategorySelected('user.*')).toBe(true);
    });

    it('toggleCategory removes a category and falls back to "*" when none remain', () => {
      component.selectedEvents.set(['user.*']);
      component.toggleCategory('user.*');
      expect(component.selectedEvents()).toEqual(['*']);
    });
  });

  describe('openCreate / openEdit / closeEditor', () => {
    it('openCreate resets editor fields and opens the editor', () => {
      component.openCreate();
      expect(component.editorOpen()).toBe(true);
      expect(component.editingId()).toBeNull();
      expect(component.formName()).toBe('');
      expect(component.selectedEvents()).toEqual(['*']);
    });

    it('openEdit hydrates editor fields from the endpoint', () => {
      component.openEdit(baseWebhook);
      expect(component.editorOpen()).toBe(true);
      expect(component.editingId()).toBe('w1');
      expect(component.formName()).toBe('siem');
      expect(component.formEnabled()).toBe(true);
      expect(component.selectedEvents()).toEqual(['user.*', 'connection.*']);
    });

    it('closeEditor flips the editor closed', () => {
      component.editorOpen.set(true);
      component.closeEditor();
      expect(component.editorOpen()).toBe(false);
    });
  });

  describe('submitEditor', () => {
    it('rejects empty name/url with an error message', () => {
      component.openCreate();
      component.formName.set('');
      component.formUrl.set('');
      component.submitEditor();
      expect(component.error()).toContain('required');
      expect(svc.create).not.toHaveBeenCalled();
    });

    it('creates a new endpoint and reveals the one-time secret', () => {
      component.openCreate();
      component.formName.set('fresh');
      component.formUrl.set('https://example.com/x');
      component.selectedEvents.set(['*']);
      component.submitEditor();
      expect(svc.create).toHaveBeenCalledWith({
        name: 'fresh',
        url: 'https://example.com/x',
        eventTypes: '*',
      });
      expect(component.revealedSecret()?.secret).toBe('whsec_new');
      expect(component.editorOpen()).toBe(false);
    });

    it('updates an existing endpoint without revealing a secret', () => {
      component.openEdit(baseWebhook);
      component.formName.set('renamed');
      component.formUrl.set('https://example.com/v2');
      component.formEnabled.set(false);
      component.selectedEvents.set(['connection.*']);
      component.submitEditor();
      expect(svc.update).toHaveBeenCalledWith('w1', {
        name: 'renamed',
        url: 'https://example.com/v2',
        eventTypes: 'connection.*',
        enabled: false,
      });
      expect(component.revealedSecret()).toBeNull();
    });

    it('surfaces a create error via the error signal', () => {
      svc.create.mockReturnValueOnce(throwError(() => ({ error: { message: 'nope' } })));
      component.openCreate();
      component.formName.set('x');
      component.formUrl.set('https://example.com');
      component.submitEditor();
      expect(component.error()).toBe('nope');
    });
  });

  describe('row actions', () => {
    it('toggleEnabled calls update with the flipped enabled flag', () => {
      component.toggleEnabled({ ...baseWebhook, enabled: true });
      expect(svc.update).toHaveBeenCalledWith('w1', expect.objectContaining({ enabled: false }));
    });

    it('sendTest queues a delivery and shows a confirmation message', () => {
      component.sendTest(baseWebhook);
      expect(svc.sendTest).toHaveBeenCalledWith('w1');
      expect(component.message()).toContain('siem');
    });

    it('requestDelete asks for confirmation then deletes', async () => {
      await component.requestDelete(baseWebhook);
      expect(confirm.ask).toHaveBeenCalled();
      expect(svc.remove).toHaveBeenCalledWith('w1');
    });

    it('requestDelete skips deletion when confirmation is declined', async () => {
      confirm.ask.mockResolvedValueOnce(false);
      await component.requestDelete(baseWebhook);
      expect(svc.remove).not.toHaveBeenCalled();
    });

    it('requestRotate confirms then reveals the new secret', async () => {
      await component.requestRotate(baseWebhook);
      expect(svc.rotateSecret).toHaveBeenCalledWith('w1');
      expect(component.revealedSecret()?.secret).toBe('whsec_rot');
    });
  });

  describe('menu state', () => {
    it('toggleMenu opens and closes the kebab for a given id', () => {
      component.toggleMenu('w1');
      expect(component.openMenuId()).toBe('w1');
      component.toggleMenu('w1');
      expect(component.openMenuId()).toBeNull();
    });

    it('closeMenu clears the open id', () => {
      component.openMenuId.set('w1');
      component.closeMenu();
      expect(component.openMenuId()).toBeNull();
    });
  });

  describe('secret reveal', () => {
    it('dismissSecret clears the revealed secret', () => {
      component.revealedSecret.set({ id: 'x', name: 'n', secret: 's' });
      component.dismissSecret();
      expect(component.revealedSecret()).toBeNull();
    });
  });

  describe('display helpers', () => {
    it('eventSummary returns "All events" for the wildcard', () => {
      expect(component.eventSummary('*')).toBe('All events');
    });

    it('eventSummary joins category labels', () => {
      expect(component.eventSummary('user.*,connection.*')).toBe(
        'Users & authentication, Connections',
      );
    });

    it('statusText reports health states', () => {
      expect(component.statusText({ ...baseWebhook, enabled: false })).toBe('Disabled');
      expect(component.statusText({ ...baseWebhook, lastDeliveryStatus: undefined })).toBe(
        'No deliveries yet',
      );
      expect(component.statusText({ ...baseWebhook, lastDeliveryStatus: 'success' })).toBe(
        'Healthy',
      );
      expect(
        component.statusText({
          ...baseWebhook,
          lastDeliveryStatus: 'failed',
          consecutiveFailures: 3,
        }),
      ).toBe('Failing (3)');
    });

    it('statusTone reports semantic tones', () => {
      expect(component.statusTone({ ...baseWebhook, enabled: false })).toBe('idle');
      expect(component.statusTone({ ...baseWebhook, lastDeliveryStatus: 'success' })).toBe('ok');
      expect(component.statusTone({ ...baseWebhook, lastDeliveryStatus: 'failed' })).toBe('bad');
    });
  });
});
