/**
 * BuildingPopup — In-scene popup overlay when a building is clicked.
 *
 * Shows building info, current production, upgrade cost, and an upgrade button.
 */
import Phaser from 'phaser';
import type { BuildingSlot, ResourceState } from '@village-types/village-data';
import { BuildingType, ResourceType } from '@village-types/village-data';
import {
  COLORS,
  BUILDING_ICONS,
  RESOURCE_ICONS,
  productionPerSecond,
  upgradeCostForLevel,
} from '@village-shared/constants';
import { formatNumber, formatProduction } from '@village-shared/village-utils';

const POPUP_W = 260;
const POPUP_H = 280;

export class BuildingPopup {
  private container: Phaser.GameObjects.Container;
  private scene: Phaser.Scene;

  constructor(
    scene: Phaser.Scene,
    x: number,
    y: number,
    building: BuildingSlot,
    resources: ResourceState[],
    onUpgrade: (buildingId: number) => void,
  ) {
    this.scene = scene;

    // Clamp popup inside camera view
    const cam = scene.cameras.main;
    const halfW = POPUP_W / 2;
    const halfH = POPUP_H / 2;
    const clampedX = Phaser.Math.Clamp(x, cam.scrollX + halfW + 10, cam.scrollX + cam.width / cam.zoom - halfW - 10);
    const clampedY = Phaser.Math.Clamp(y, cam.scrollY + halfH + 10, cam.scrollY + cam.height / cam.zoom - halfH - 10);

    this.container = scene.add.container(clampedX, clampedY);
    this.container.setDepth(100);

    // Background panel
    const bg = scene.add.graphics();
    bg.fillStyle(COLORS.popupBg, 0.95);
    bg.fillRoundedRect(-POPUP_W / 2, -POPUP_H / 2, POPUP_W, POPUP_H, 12);
    bg.lineStyle(3, COLORS.popupBorder, 1);
    bg.strokeRoundedRect(-POPUP_W / 2, -POPUP_H / 2, POPUP_W, POPUP_H, 12);
    this.container.add(bg);

    // Close button (X)
    const closeBtn = scene.add.text(POPUP_W / 2 - 20, -POPUP_H / 2 + 8, '✕', {
      fontSize: '18px',
      color: '#ff6666',
      fontStyle: 'bold',
    }).setOrigin(0.5).setInteractive({ useHandCursor: true });
    closeBtn.on('pointerup', () => this.destroy());
    this.container.add(closeBtn);

    let yOff = -POPUP_H / 2 + 22;

    // Building icon & name
    const icon = BUILDING_ICONS[building.type];
    const title = scene.add.text(0, yOff, `${icon} ${building.name}`, {
      fontSize: '16px',
      fontFamily: 'Arial, sans-serif',
      color: '#ffd700',
      fontStyle: 'bold',
      align: 'center',
    }).setOrigin(0.5, 0);
    this.container.add(title);
    yOff += 26;

    // Level
    const levelTxt = scene.add.text(0, yOff, `Nível ${building.level}`, {
      fontSize: '14px',
      fontFamily: 'Arial, sans-serif',
      color: '#ffffff',
      align: 'center',
    }).setOrigin(0.5, 0);
    this.container.add(levelTxt);
    yOff += 22;

    // Description
    const desc = scene.add.text(0, yOff, building.description, {
      fontSize: '11px',
      fontFamily: 'Arial, sans-serif',
      color: '#aaaaaa',
      align: 'center',
      wordWrap: { width: POPUP_W - 30 },
    }).setOrigin(0.5, 0);
    this.container.add(desc);
    yOff += desc.height + 10;

    // Production info (for resource buildings)
    const resMap: Record<string, ResourceType> = {
      [BuildingType.LumberMill]: ResourceType.Wood,
      [BuildingType.ClayPit]: ResourceType.Clay,
      [BuildingType.IronMine]: ResourceType.Iron,
      [BuildingType.Farm]: ResourceType.Crop,
    };
    const resType = resMap[building.type];
    if (resType) {
      const prodNow = productionPerSecond(building.level);
      const prodNext = productionPerSecond(building.level + 1);
      const prodLabel = scene.add.text(0, yOff,
        `Produção: ${formatProduction(prodNow)} → ${formatProduction(prodNext)}`, {
        fontSize: '12px',
        fontFamily: 'Arial, sans-serif',
        color: '#88cc88',
        align: 'center',
      }).setOrigin(0.5, 0);
      this.container.add(prodLabel);
      yOff += 20;
    }

    // Separator
    const sep = scene.add.graphics();
    sep.lineStyle(1, 0x555555, 0.6);
    sep.beginPath();
    sep.moveTo(-POPUP_W / 2 + 15, yOff + 4);
    sep.lineTo(POPUP_W / 2 - 15, yOff + 4);
    sep.strokePath();
    this.container.add(sep);
    yOff += 12;

    // Upgrade cost
    const cost = upgradeCostForLevel(building.upgradeCost, building.level);
    const costEntries: [string, number, number][] = [
      [RESOURCE_ICONS[ResourceType.Wood], cost.wood, resources.find(r => r.type === ResourceType.Wood)!.amount],
      [RESOURCE_ICONS[ResourceType.Clay], cost.clay, resources.find(r => r.type === ResourceType.Clay)!.amount],
      [RESOURCE_ICONS[ResourceType.Iron], cost.iron, resources.find(r => r.type === ResourceType.Iron)!.amount],
      [RESOURCE_ICONS[ResourceType.Crop], cost.crop, resources.find(r => r.type === ResourceType.Crop)!.amount],
    ];

    const costLabel = scene.add.text(0, yOff, 'Custo de melhoria:', {
      fontSize: '11px',
      fontFamily: 'Arial, sans-serif',
      color: '#cccccc',
      align: 'center',
    }).setOrigin(0.5, 0);
    this.container.add(costLabel);
    yOff += 16;

    let canAfford = true;
    const costStartX = -POPUP_W / 2 + 25;
    const costColW = (POPUP_W - 50) / 4;

    for (let i = 0; i < costEntries.length; i++) {
      const [ico, needed, have] = costEntries[i];
      const enough = have >= needed;
      if (!enough) canAfford = false;
      const color = enough ? '#88ff88' : '#ff6666';
      const cx = costStartX + i * costColW + costColW / 2;
      const costTxt = scene.add.text(cx, yOff, `${ico}\n${formatNumber(needed)}`, {
        fontSize: '11px',
        fontFamily: 'Arial, sans-serif',
        color,
        align: 'center',
      }).setOrigin(0.5, 0);
      this.container.add(costTxt);
    }
    yOff += 35;

    // Upgrade button
    const btnW = 140;
    const btnH = 32;
    const btnColor = canAfford ? COLORS.upgradeBtn : COLORS.disabledBtn;
    const btn = scene.add.graphics();
    btn.fillStyle(btnColor, 1);
    btn.fillRoundedRect(-btnW / 2, yOff, btnW, btnH, 8);
    if (canAfford) {
      btn.lineStyle(2, 0x66dd66, 0.6);
      btn.strokeRoundedRect(-btnW / 2, yOff, btnW, btnH, 8);
    }
    this.container.add(btn);

    const btnLabel = scene.add.text(0, yOff + btnH / 2, canAfford ? '⬆ Melhorar' : '🔒 Sem recursos', {
      fontSize: '13px',
      fontFamily: 'Arial, sans-serif',
      color: canAfford ? '#ffffff' : '#999999',
      fontStyle: 'bold',
      align: 'center',
    }).setOrigin(0.5);
    this.container.add(btnLabel);

    if (canAfford) {
      // Make button interactive
      const hitZone = scene.add.zone(0, yOff + btnH / 2, btnW, btnH).setInteractive({ useHandCursor: true });
      hitZone.on('pointerup', () => {
        onUpgrade(building.id);
      });
      hitZone.on('pointerover', () => {
        btn.clear();
        btn.fillStyle(COLORS.upgradeBtnHover, 1);
        btn.fillRoundedRect(-btnW / 2, yOff, btnW, btnH, 8);
        btn.lineStyle(2, 0x88ff88, 0.8);
        btn.strokeRoundedRect(-btnW / 2, yOff, btnW, btnH, 8);
      });
      hitZone.on('pointerout', () => {
        btn.clear();
        btn.fillStyle(COLORS.upgradeBtn, 1);
        btn.fillRoundedRect(-btnW / 2, yOff, btnW, btnH, 8);
        btn.lineStyle(2, 0x66dd66, 0.6);
        btn.strokeRoundedRect(-btnW / 2, yOff, btnW, btnH, 8);
      });
      this.container.add(hitZone);
    }

    // Appear animation
    this.container.setScale(0.5);
    this.container.setAlpha(0);
    scene.tweens.add({
      targets: this.container,
      scaleX: 1,
      scaleY: 1,
      alpha: 1,
      duration: 200,
      ease: 'Back.easeOut',
    });
  }

  destroy() {
    if (this.container) {
      this.scene.tweens.add({
        targets: this.container,
        scaleX: 0.5,
        scaleY: 0.5,
        alpha: 0,
        duration: 150,
        ease: 'Cubic.easeIn',
        onComplete: () => {
          this.container.destroy();
        },
      });
    }
  }
}
