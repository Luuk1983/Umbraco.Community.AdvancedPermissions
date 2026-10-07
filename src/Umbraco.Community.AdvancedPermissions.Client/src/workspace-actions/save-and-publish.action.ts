import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { createExtensionApiByAlias } from '@umbraco-cms/backoffice/extension-registry';
import { UmbWorkspaceActionBase, type UmbWorkspaceActionArgs } from '@umbraco-cms/backoffice/workspace';
import {
  UMB_DOCUMENT_PUBLISHING_WORKSPACE_CONTEXT,
  UMB_DOCUMENT_WORKSPACE_CONTEXT,
  UMB_USER_PERMISSION_DOCUMENT_PUBLISH,
  UMB_USER_PERMISSION_DOCUMENT_UPDATE,
} from '@umbraco-cms/backoffice/document';
import { gateOnDocumentPermission } from './document-permission-gate.js';

/** The executing-state feedback Umbraco 17.5 added to workspace actions; absent on 17.3 and 17.4. */
type ExecutingFeedback = { setExecuting?: (executing: boolean) => void };

/** <c>saveAndPublish</c> as of Umbraco 17.5, which reports when the publish actually starts. */
type SaveAndPublish = (args?: { onActionStarting?: () => void }) => Promise<void>;

/**
 * Replacement api for Umbraco's "Save and publish" document workspace action
 * (<c>Umb.WorkspaceAction.Document.SaveAndPublish</c>).
 *
 * A copy of Umbraco 17's <c>UmbDocumentSaveAndPublishWorkspaceAction</c> with one difference: the
 * native action constructs <c>UmbDocumentUserPermissionCondition</c> directly, bypassing the
 * extension registry, so the Advanced Permissions condition registered under
 * <c>Umb.Condition.UserPermission.Document</c> never decides it. The native check reads the user
 * group's native default permissions instead, which this package hides from the user group editor:
 * the button can be enabled where Advanced Permissions denies publishing (the server then refuses
 * the request) and disabled where it allows it. This copy resolves the condition by alias instead,
 * so the per-node Advanced Permissions verdict decides the button.
 *
 * The executing-state feedback from Umbraco 17.5 is used when present, so the button behaves like
 * the installed native action on every supported 17.x version.
 *
 * Keep in step with the native action on Umbraco upgrades; remove once Umbraco resolves the
 * condition by alias itself.
 */
export class UapDocumentSaveAndPublishWorkspaceAction extends UmbWorkspaceActionBase {
  /**
   * Creates the action, disabled until the document permission condition grants Update and Publish.
   * @param host The controller host (the workspace action element).
   * @param args The workspace action arguments from the manifest.
   */
  constructor(host: UmbControllerHost, args: UmbWorkspaceActionArgs<never>) {
    super(host, args);

    // Opt in to isExecuting feedback (Umbraco 17.5+) so the workspace-action element waits
    // for the variant-picker modal (when present) before showing the spinner.
    this.#setExecuting(false);

    void gateOnDocumentPermission(createExtensionApiByAlias, this, this, [
      UMB_USER_PERMISSION_DOCUMENT_UPDATE,
      UMB_USER_PERMISSION_DOCUMENT_PUBLISH,
    ]);
  }

  /**
   * Whether the button offers a variant picker: true when the document has more than one culture.
   * @returns True when there is more than one culture variant to choose from.
   */
  async hasAdditionalOptions(): Promise<boolean> {
    const workspaceContext = await this.getContext(UMB_DOCUMENT_WORKSPACE_CONTEXT);
    if (!workspaceContext) {
      throw new Error('The workspace context is missing');
    }
    const variantOptions = await this.observe(workspaceContext.variantOptions).asPromise();
    const cultureVariantOptions = variantOptions?.filter((option) => option.segment === null);
    return cultureVariantOptions?.length > 1;
  }

  /**
   * Saves and publishes the document through the document publishing workspace context.
   * @returns A promise settling when the save and publish has finished.
   */
  override async execute(): Promise<void> {
    try {
      const workspaceContext = await this.getContext(UMB_DOCUMENT_PUBLISHING_WORKSPACE_CONTEXT);
      if (!workspaceContext) {
        throw new Error('The workspace context is missing');
      }
      // Umbraco 17.3 and 17.4 take no arguments here and ignore the callback.
      const saveAndPublish: SaveAndPublish = workspaceContext.saveAndPublish.bind(workspaceContext);
      await saveAndPublish({
        onActionStarting: () => this.#setExecuting(true),
      });
    } finally {
      this.#setExecuting(false);
    }
  }

  /**
   * Reports the executing state through Umbraco's workspace action feedback when the installed
   * Umbraco version has it (17.5+); a no-op on 17.3 and 17.4.
   * @param executing Whether the action is executing.
   */
  #setExecuting(executing: boolean): void {
    (this as ExecutingFeedback).setExecuting?.(executing);
  }
}

export { UapDocumentSaveAndPublishWorkspaceAction as api };
