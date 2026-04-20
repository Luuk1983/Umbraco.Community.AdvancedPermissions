import { UmbModalToken } from '@umbraco-cms/backoffice/modal';
import type { UserItem } from '../models/permission.models.js';

export interface UserPickerModalData {
  currentUser?: string;
}

export interface UserPickerModalValue {
  user: UserItem;
}

export const UAP_USER_PICKER_MODAL = new UmbModalToken<UserPickerModalData, UserPickerModalValue>(
  'UAP.Modal.UserPicker',
  { modal: { type: 'sidebar', size: 'small' } },
);
