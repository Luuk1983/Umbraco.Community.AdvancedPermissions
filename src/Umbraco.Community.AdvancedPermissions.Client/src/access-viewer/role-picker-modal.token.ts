import { UmbModalToken } from '@umbraco-cms/backoffice/modal';
import type { RoleInfo } from '../models/permission.models.js';

export interface RolePickerModalData {
  currentRole?: string;
}

export interface RolePickerModalValue {
  role: RoleInfo;
}

export const UAP_ROLE_PICKER_MODAL = new UmbModalToken<RolePickerModalData, RolePickerModalValue>(
  'UAP.Modal.RolePicker',
  { modal: { type: 'sidebar', size: 'small' } },
);
