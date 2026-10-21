import { Routes } from '@angular/router';
import { ActivityDetail } from './activity-detail';
import { RunningTrends } from './running-trends';
import { DataManagement } from './data-management';

export const routes: Routes = [
  { path: 'trends', component: RunningTrends },
  { path: 'data', component: DataManagement },
  { path: 'activities/:id', component: ActivityDetail, data: { presentation: 'drawer' } },
  { path: 'activity/:id', component: ActivityDetail, data: { presentation: 'page' } },
];
