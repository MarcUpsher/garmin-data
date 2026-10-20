import { Routes } from '@angular/router';
import { ActivityDetail } from './activity-detail';

export const routes: Routes = [
  { path: 'activities/:id', component: ActivityDetail, data: { presentation: 'drawer' } },
  { path: 'activity/:id', component: ActivityDetail, data: { presentation: 'page' } },
];
