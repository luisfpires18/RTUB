@page "/events"
@rendermode InteractiveServer
@using Microsoft.AspNetCore.Components.Authorization
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Identity
@using RTUB.Application.Helpers
@using RTUB.Application.Extensions
@using RTUB.Application.Interfaces
@using RTUB.Shared
@inject IEventService EventService
@inject IEnrollmentService EnrollmentService
@inject ITrophyService TrophyService
@inject AuthenticationStateProvider AuthenticationStateProvider
@inject UserManager<ApplicationUser> UserManager
@inject IWebHostEnvironment Environment

<h1>Atuações</h1>
<p>Próximas atuações e atividades.</p>

<AuthorizeView Roles="Admin">
    <Authorized>
        <div class="mb-3">
            <button class="btn btn-success" @onclick="OpenCreateModal" title="Adicionar Novo Evento">
                <i class="bi bi-plus-lg"></i>
            </button>
        </div>
    </Authorized>
</AuthorizeView>

@if (events == null)
{
    <p>A carregar atuações...</p>
}
else if (!events.Any())
{
    <div class="card shadow-sm card-purple-no-border">
        <h5 class="card-title text-white"><i class="bi bi-info-circle"></i>  Nenhum evento criado ainda</h5>
    </div>
}
else
{
    <div class="row">
        @foreach (var eventItem in events)
        {
            <div class="col-md-4 mb-4">
                <AuthorizeView>
                    <Authorized>
                        @{
                            var userEnrollment = GetUserEnrollment(eventItem.Id);
                            var isAdmin = context.User.IsInRole("Admin") || context.User.IsInRole("Owner");
                        }
                        <EventCard Event="@eventItem"
                                   UserEnrollment="@userEnrollment"
                                   EnrollmentCount="@GetEnrollmentCount(eventItem.Id)"
                                   IsAdmin="@isAdmin"
                                   IsPastEvent="false"
                                   OnEdit="@(() => OpenEditModal(eventItem))"
                                   OnDelete="@(() => OpenDeleteModal(eventItem))"
                                   OnEnroll="@(() => OpenEnrollModal(eventItem))"
                                   OnViewEnrollments="@(() => OpenEnrollmentListModal(eventItem))"
                                   OnEditEnrollment="@(() => OpenEditEnrollmentModal(userEnrollment))"
                                   OnRemoveEnrollment="@(() => OpenRemoveEnrollmentModal(userEnrollment))"
                                   OnWatchRepertoire="@(() => OpenRepertoireModal(eventItem))"
                                   OnViewDetails="@(() => OpenEventDetailsModal(eventItem))" />
                    </Authorized>
                    <NotAuthorized>
                        <EventCard Event="@eventItem"
                                   EnrollmentCount="@GetEnrollmentCount(eventItem.Id)"
                                   IsAdmin="false"
                                   IsPastEvent="false"
                                   OnViewEnrollments="@(() => OpenEnrollmentListModal(eventItem))"
                                   OnWatchRepertoire="@(() => OpenRepertoireModal(eventItem))"
                                   OnViewDetails="@(() => OpenEventDetailsModal(eventItem))" />
                    </NotAuthorized>
                </AuthorizeView>
            </div>
        }
    </div>
}

<!-- Admin: Previous Events Section -->
<AuthorizeView Roles="Admin,Owner">
    <Authorized>
        <hr class="my-5" />

        <div class="d-flex justify-content-between align-items-center mb-3">
            <h2>Atuações Anteriores</h2>
        </div>

        <TableSearchBar SearchTerm="@previousEventsSearch"
                        Placeholder="Pesquisar atuações anteriores..."
                       OnSearchChanged="@HandlePreviousEventsSearch" />

        @if (filteredPreviousEvents == null)
        {
            <p>A carregar atuações anteriores...</p>
        }
        else if (!filteredPreviousEvents.Any())
        {
            <EmptyTableState Message="Nenhum evento anterior encontrado"
                             SubMessage="Não existem atuações passados para mostrar."
                           IconClass="bi-calendar-x" />
        }
        else
        {
            <div class="row">
                @foreach (var eventItem in paginatedPreviousEvents)
                {
                    <div class="col-md-4 mb-4">
                        <div class="card event-card h-100">
                            <div class="position-relative">
                                @if (!string.IsNullOrEmpty(eventItem.ImageSrc))
                                {
                                    <img src="@GetEventImageUrl(eventItem.ImageSrc)" class="card-img-top event-image" alt="@eventItem.Name">
                                }
                                else
                                {
                                    <div class="card-img-top event-image-placeholder">
                                        <i class="bi bi-calendar-event music-icon-large"></i>
                                    </div>
                                }
                                <AuthorizeView Roles="Owner">
                                    <Authorized Context="editContext">
                                        <div class="admin-overlay">
                                            <button class="btn btn-sm music-btn-edit" @onclick="() => OpenEditModal(eventItem)" title="Editar">
                                                <i class="bi bi-pencil music-icon-edit"></i>
                                            </button>
                                            <button class="btn btn-sm music-btn-delete" @onclick="() => OpenDeleteModal(eventItem)" title="Eliminar">
                                                <i class="bi bi-trash music-icon-delete"></i>
                                            </button>
                                        </div>
                                    </Authorized>
                                </AuthorizeView>
                            </div>
                            <div class="card-body d-flex flex-column p-3">
                                <h5 class="card-title mb-2">@eventItem.Name</h5>
                                <p class="card-text text-light-theme mb-2">
                                    <small>
                                        <i class="bi bi-calendar me-1"></i>
                                        @if (eventItem.EndDate.HasValue && eventItem.EndDate.Value.Date != eventItem.Date.Date)
                                        {
                                            <span>@eventItem.Date.ToString("dd MMM") - @eventItem.EndDate.Value.ToString("dd MMM yyyy")</span>
                                        }
                                        else
                                        {
                                            <span>@eventItem.Date.ToString("dd MMM yyyy")</span>
                                        }
                                    </small>
                                </p>
                                @if (!string.IsNullOrEmpty(eventItem.Location))
                                {
                                    <p class="card-text text-light-theme mb-2">
                                        <small>
                                            <i class="bi bi-geo-alt me-1"></i> @eventItem.Location
                                        </small>
                                    </p>
                                }
                                @if (!string.IsNullOrEmpty(eventItem.Description))
                                {
                                    <p class="card-text flex-grow-1 mb-2">@eventItem.Description</p>
                                }

                                <div class="admin-section mt-auto p-2 rounded">
                                    <small class="text-muted d-block mb-2 text-center"><i class="bi bi-clock-history"></i> Evento Passado</small>
                                    <div class="d-flex gap-2 justify-content-center flex-wrap">
                                        <!-- View Enrollments button -->
                                        <button class="btn btn-sm btn-outline-secondary d-flex align-items-center gap-1" 
                                                @onclick="() => OpenEnrollmentListModal(eventItem)" 
                                                title="Ver Inscrições">
                                            <i class="bi bi-people icon-size-1rem"></i>
                                            <span>@GetEnrollmentCount(eventItem.Id)</span>
                                        </button>
                                        
                                        <!-- Watch Repertoire button -->
                                        <button class="btn btn-sm btn-outline-primary d-flex align-items-center" 
                                                @onclick="() => OpenRepertoireModal(eventItem)" 
                                                title="Ver Repertório">
                                            <i class="bi bi-music-note-list icon-size-1rem"></i>
                                        </button>
                                        
                                        @if (eventItem.Type == EventType.Festival)
                                        {
                                            <!-- Trophy button -->
                                            <button class="btn btn-sm btn-warning d-flex align-items-center" 
                                                    @onclick="() => OpenTrophyModal(eventItem)" 
                                                    title="Ver Prémios">
                                                <i class="bi bi-trophy-fill icon-size-1rem"></i>
                                            </button>
                                        }
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                }
            </div>

            <TablePagination CurrentPage="@paginationHelper.CurrentPage"
                            PageSize="@paginationHelper.PageSize"
                            TotalItems="@(filteredPreviousEvents?.Count ?? 0)"
                            ItemLabel="eventos"
                            OnPageChanged="@HandlePreviousEventsPageChange"
                            OnPageSizeChanged="@HandlePreviousEventsPageSizeChange" />
        }
    </Authorized>
</AuthorizeView>

<!-- Edit/Create Modal -->
<Modal Show="@showEditModal" 
       ShowChanged="@((bool show) => showEditModal = show)"
       Title="@(isCreateMode ? "Criar Novo Evento" : "Editar Evento")" 
       Size="Modal.ModalSize.Default"
       Centered="true">
    <BodyContent>
        <EditForm Model="editingEvent" OnValidSubmit="SaveEvent">
            <DataAnnotationsValidator />
            <ValidationSummary class="text-danger" />

            <div class="mb-3">
                <label class="form-label">Nome da Atuação</label>
                <InputText class="form-control" @bind-Value="editingEvent!.Name" />
            </div>

            <div class="row">
                <div class="col-md-6 mb-3">
                    <label class="form-label">Data Início</label>
                    @if (isCreateMode)
                    {
                        <InputDate class="form-control" @bind-Value="editingEvent!.Date" min="@DateTime.Today.AddDays(1).ToString("yyyy-MM-dd")" />
                    }
                    else
                    {
                        <InputDate class="form-control" @bind-Value="editingEvent!.Date" />
                    }
                    @if (!string.IsNullOrEmpty(dateValidationError))
                    {
                        <div class="text-danger small mt-1">@dateValidationError</div>
                    }
                </div>
                <div class="col-md-6 mb-3">
                    <label class="form-label">Data Fim (opcional)</label>
                    <InputDate class="form-control" @bind-Value="editingEvent!.EndDate" />
                </div>
            </div>

            <div class="mb-3">
                <label class="form-label">Localização</label>
                <InputText class="form-control" @bind-Value="editingEvent!.Location" />
            </div>

            <div class="mb-3">
                <label class="form-label">Tipo de Atuação</label>
                <InputSelect class="form-select" @bind-Value="editingEvent!.Type">
                    @foreach (var type in Enum.GetValues<EventType>())
                    {
                        <option value="@type">@GetEventTypeDisplay(type)</option>
                    }
                </InputSelect>
            </div>

            <ImageUploadManager @ref="imageUploadManager"
                               Label="Imagem do Evento"
                               CurrentImageUrl="@(uploadedImagePath == null && !string.IsNullOrEmpty(editingEvent!.ImageSrc) ? GetEventImageUrl(editingEvent.ImageSrc) : null)"
                               ShowCurrentImage="@(uploadedImagePath == null)"
                               PreviewCssClass="slideshow-preview"
                               OnFileSelected="HandleImageUpload" />

            <div class="mb-3">
                <label class="form-label">Descrição</label>
                <InputTextArea class="form-control" rows="4" @bind-Value="editingEvent!.Description" />
            </div>

            <div class="d-flex justify-content-end gap-2">
                <button type="button" class="btn btn-secondary" @onclick="CloseEditModal">Cancelar</button>
                <button type="submit" class="btn btn-primary btn-primary-purple">Guardar</button>
            </div>
        </EditForm>
    </BodyContent>
</Modal>

<!-- Delete Confirmation Modal -->
<Modal Show="@showDeleteModal" 
       ShowChanged="@((bool show) => showDeleteModal = show)"
       Title="Confirmar Eliminação" 
       Size="Modal.ModalSize.Default"
       Centered="true">
    <BodyContent>
        <p>Tem a certeza de que pretende eliminar a atuação <strong>@deletingEvent?.Name</strong>?</p>
        <p class="text-muted">Esta ação não pode ser anulada..</p>
    </BodyContent>
    <FooterContent>
        <button type="button" class="btn btn-secondary" @onclick="CloseDeleteModal">Cancelar</button>
        <button type="button" class="btn btn-danger" @onclick="DeleteEvent">Eliminar</button>
    </FooterContent>
</Modal>

<!-- Enrollment Modal -->
@if (enrollmentForm != null)
{
    <Modal Show="@showEnrollModal" 
           ShowChanged="@((bool show) => showEnrollModal = show)"
           Title="@(isEditingEnrollment ? "Editar Inscrição" : $"Inscrever em {selectedEvent?.Name}")" 
           Size="Modal.ModalSize.Default"
           Centered="true">
        <BodyContent>
            <EditForm Model="enrollmentForm" OnValidSubmit="SaveEnrollment">
                <DataAnnotationsValidator />
                <ValidationSummary class="text-danger" />

                @* Only show "Quero tocar" toggle for non-Leitão members *@
                @if (currentUser != null && !currentUser.Categories.Contains(MemberCategory.Leitao))
                {
                    <div class="mb-3 form-check form-switch">
                        <input class="form-check-input clickable-row" type="checkbox" id="wantToPlayToggle"
                               checked="@wantToPlay"
                               @onchange="@((ChangeEventArgs e) => OnWantToPlayChanged((bool)e.Value!))">
                        <label class="form-check-label form-check-label-white-clickable" for="wantToPlayToggle">
                            Quero tocar
                        </label>
                    </div>

                    @if (wantToPlay)
                    {
                        <div class="mb-3">
                            <label class="form-label text-white-full">Instrumento *</label>
                            <InputSelect class="form-select form-select-dark" @bind-Value="enrollmentForm.Instrument">
                                <option value="">Selecionar...</option>
                                @foreach (var instrument in Enum.GetValues<InstrumentType>())
                                {
                                    <option value="@instrument">@StatusHelper.GetInstrumentDisplay(instrument)</option>
                                }
                            </InputSelect>
                        </div>
                    }
                }

                <div class="mb-3">
                    <label class="form-label text-white-full">Notas (opcional)</label>
                    <InputTextArea class="form-control" rows="4" @bind-Value="enrollmentForm.Notes" placeholder="Informações adicionais..." />
                </div>

                <div class="d-flex justify-content-end gap-2">
                    <button type="button" class="btn btn-secondary" @onclick="CloseEnrollModal">Cancelar</button>
                    <button type="submit" class="btn btn-primary btn-primary-purple">@(isEditingEnrollment ? "Atualizar" : "Inscrever")</button>
                </div>
            </EditForm>
        </BodyContent>
    </Modal>
}

<!-- Remove Enrollment Confirmation Modal -->
<Modal Show="@showRemoveEnrollmentModal" 
       ShowChanged="@((bool show) => showRemoveEnrollmentModal = show)"
       Title="Confirmar Cancelamento de Inscrição" 
       Size="Modal.ModalSize.Default"
       Centered="true">
    <BodyContent>
        <p>Tem certeza que deseja cancelar sua inscrição neste evento?</p>
    </BodyContent>
    <FooterContent>
        <button type="button" class="btn btn-secondary" @onclick="CloseRemoveEnrollmentModal">Não</button>
        <button type="button" class="btn btn-danger" @onclick="RemoveEnrollment">Sim, Cancelar</button>
    </FooterContent>
</Modal>

<!-- View Enrollments Modal -->
@if (selectedEvent != null)
{
    <Modal Show="@showEnrollmentListModal" 
           ShowChanged="@((bool show) => showEnrollmentListModal = show)"
           Title="@($"Inscrições em {selectedEvent.Name}")" 
           Size="Modal.ModalSize.Large"
           Centered="true">
        <BodyContent>
            @if (eventEnrollments == null || !eventEnrollments.Any())
            {
                <p class="text-white-full">Nenhuma inscrição ainda para este evento.</p>
            }
            else
            {
                var performingMembers = eventEnrollments.Where(e => e.User != null && !e.User.Categories.Contains(MemberCategory.Leitao)).ToList();
                var leitaoMembers = eventEnrollments.Where(e => e.User != null && e.User.Categories.Contains(MemberCategory.Leitao)).ToList();

                @if (performingMembers.Any())
                {
                    <h6 class="mb-3 heading-purple">Membros (@performingMembers.Count)</h6>
                    
                    <!-- Search Control -->
                    <div class="mb-3">
                        <input type="text" class="form-control" placeholder="Pesquisar por nome..." 
                               @bind="enrollmentSearchTerm" @bind:event="oninput" @onkeyup="FilterEnrollments" />
                    </div>
                    
                    <div class="enrollment-card-grid mb-4">
                        @foreach (var enrollment in paginatedPerformingMembers)
                        {
                            <EnrollmentCard 
                                AvatarUrl="@enrollment.User?.ProfilePictureSrc"
                                TunaName="@enrollment.User?.Nickname"
                                FullName="@($"{enrollment.User?.FirstName} {enrollment.User?.LastName}")"
                                InstrumentText="@(enrollment.Instrument.HasValue ? StatusHelper.GetInstrumentDisplay(enrollment.Instrument.Value) : "")"
                                Notes="@enrollment.Notes"
                                AltText="@enrollment.User?.GetDisplayName()"
                                ShowEditButton="false"
                                ShowDeleteButton="@isUserOwner"
                                OnDelete="@(() => OpenDeleteEnrollmentModal(enrollment))"
                                DeleteTooltip="Eliminar Inscrição">
                                <BadgeContent>
                                    @if (enrollment.User != null)
                                    {
                                        <div class="enrollment-card-badges-row">
                                            @if (enrollment.User.Positions.Contains(Position.Magister))
                                            {
                                                <PositionBadge Position="Position.Magister" />
                                            }
                                            else if (enrollment.User.Categories.Contains(MemberCategory.Tuno))
                                            {
                                                <CategoryBadge Category="MemberCategory.Tuno" />
                                            }
                                            else if (enrollment.User.Categories.Contains(MemberCategory.Caloiro))
                                            {
                                                <CategoryBadge Category="MemberCategory.Caloiro" />
                                            }
                                        </div>
                                    }
                                </BadgeContent>
                            </EnrollmentCard>
                        }
                    </div>

                    <!-- Pagination for Performing Members -->
                    @if (filteredPerformingMembers.Count > 0)
                    {
                        <div class="mb-3">
                            <TablePagination 
                                TotalItems="@filteredPerformingMembers.Count"
                                CurrentPage="@performingMembersPagination.CurrentPage"
                                PageSize="@performingMembersPagination.PageSize"
                                PageSizeOptions="@(new[] { 4, 8, 12, 16 })"
                                OnPageChanged="@HandlePerformingMembersPageChange"
                                OnPageSizeChanged="@HandlePerformingMembersPageSizeChange" />
                        </div>
                    }

                    @* Instrument Counts - Horizontal Counter Section *@
                    var instrumentCounts = new Dictionary<InstrumentType, int>();
                    foreach (var inst in Enum.GetValues<InstrumentType>())
                    {
                        instrumentCounts[inst] = performingMembers.Count(e => e.Instrument == inst);
                    }

                    <div class="mt-3 mb-4">
                        <div class="instrument-counter-section">
                            <div class="instrument-counter-header">
                                <i class="bi bi-music-note-beamed"></i>
                                <span>Instrumentos</span>
                            </div>
                            <div class="instrument-counter-grid">
                                @foreach (var kvp in instrumentCounts.Where(x => x.Value > 0).OrderByDescending(x => x.Value))
                                {
                                    <div class="instrument-counter-box">
                                        <div class="instrument-counter-number">@kvp.Value</div>
                                        <div class="instrument-counter-label">@StatusHelper.GetInstrumentDisplay(kvp.Key)</div>
                                    </div>
                                }
                            </div>
                        </div>
                    </div>
                }

                @if (leitaoMembers.Any())
                {
                    <h6 class="mb-3 heading-warning">Leitões (@leitaoMembers.Count)</h6>
                    <div class="enrollment-card-grid">
                        @foreach (var enrollment in paginatedLeitoes)
                        {
                            <EnrollmentCard 
                                AvatarUrl="@enrollment.User?.ProfilePictureSrc"
                                TunaName="@enrollment.User?.Nickname"
                                FullName="@($"{enrollment.User?.FirstName} {enrollment.User?.LastName}")"
                                InstrumentText=""
                                Notes="@enrollment.Notes"
                                AltText="@enrollment.User?.GetDisplayName()"
                                ShowEditButton="false"
                                ShowDeleteButton="@isUserOwner"
                                OnDelete="@(() => OpenDeleteEnrollmentModal(enrollment))"
                                DeleteTooltip="Eliminar Inscrição">
                                <BadgeContent>
                                    <div class="enrollment-card-badges-row">
                                        <CategoryBadge Category="MemberCategory.Leitao" />
                                    </div>
                                </BadgeContent>
                            </EnrollmentCard>
                        }
                    </div>
                    
                    <!-- Pagination for Leitões -->
                    @if (filteredLeitoes.Count > 0)
                    {
                        <div class="mt-3">
                            <TablePagination 
                                TotalItems="@filteredLeitoes.Count"
                                CurrentPage="@leitoesPagination.CurrentPage"
                                PageSize="@leitoesPagination.PageSize"
                                PageSizeOptions="@(new[] { 4, 8, 12, 16 })"
                                OnPageChanged="@HandleLeitoesPageChange"
                                OnPageSizeChanged="@HandleLeitoesPageSizeChange" />
                        </div>
                    }
                }
            }
        </BodyContent>
        <FooterContent>
            <button type="button" class="btn btn-secondary" @onclick="CloseEnrollmentListModal">Fechar</button>
        </FooterContent>
    </Modal>
}

<!-- Image Cropper Component -->
<ImageCropper @ref="imageCropper"
              ShowModal="showCropperModal"
              ShowModalChanged="OnCropperModalChanged"
              OnImageCropped="OnImageCropped"
              AspectRatio="1.5"
              AspectRatioHelp="Event images are displayed in 3:2 aspect ratio for better presentation"
              ImageFormat="image/jpeg"
              ImageQuality="0.85" />

<!-- Repertoire Modal -->
<RepertoireModal Show="@showRepertoireModal"
                ShowChanged="@((bool show) => showRepertoireModal = show)"
                EventId="@(selectedEventForRepertoire?.Id ?? 0)"
                EventName="@(selectedEventForRepertoire?.Name ?? "")"
                IsAdmin="@isUserAdmin"
                OnRepertoireChanged="@HandleRepertoireChanged" />

<!-- Event Details Modal -->
@if (selectedEvent != null)
{
    <DetailsModal Show="@showEventDetailsModal"
                  ShowChanged="@((bool show) => showEventDetailsModal = show)"
                  Title="@selectedEvent.Name"
                  IconClass="bi-calendar-event"
                  Centered="true">
        <BadgesContent>
            <span class="badge bg-primary me-1">
                <i class="bi bi-calendar me-1"></i>
                @if (selectedEvent.EndDate.HasValue && selectedEvent.EndDate.Value.Date != selectedEvent.Date.Date)
                {
                    <span>@selectedEvent.Date.ToString("dd MMM") - @selectedEvent.EndDate.Value.ToString("dd MMM yyyy")</span>
                }
                else
                {
                    <span>@selectedEvent.Date.ToString("dd MMM yyyy")</span>
                }
            </span>
            @if (!string.IsNullOrEmpty(selectedEvent.Location))
            {
                <span class="badge bg-secondary">
                    <i class="bi bi-geo-alt me-1"></i>@selectedEvent.Location
                </span>
            }
        </BadgesContent>
        <BodyContent>
            @if (!string.IsNullOrEmpty(selectedEvent.Description))
            {
                <InfoSection Title="Descrição" IconClass="bi-info-circle">
                    <div style="white-space: pre-line;">@selectedEvent.Description</div>
                </InfoSection>
            }
        </BodyContent>
    </DetailsModal>
}

<!-- Trophy Modal -->
<Modal Show="@showTrophyModal"
       ShowChanged="@((bool show) => showTrophyModal = show)"
       Title="@($"Prémios  de {selectedEventForTrophies?.Name ?? ""}")"
       Size="Modal.ModalSize.Large"
       Centered="true">
    <BodyContent>
        <AuthorizeView Roles="Admin,Owner">
            <Authorized>
                <div class="mb-3">
                    <button class="btn btn-success btn-sm" @onclick="OpenCreateTrophyModal">
                        <i class="bi bi-plus-lg"></i> Adicionar Troféu
                    </button>
                </div>
            </Authorized>
        </AuthorizeView>

        @if (eventTrophies == null || !eventTrophies.Any())
        {
            <EmptyTableState Message="Nenhum troféu conquistado neste evento."
                            IconClass="bi-trophy" />
        }
        else
        {
            <div class="list-group">
                @foreach (var trophy in eventTrophies)
                {
                    <div class="list-group-item d-flex justify-content-between align-items-center">
                        <div>
                            <i class="bi bi-trophy-fill text-warning me-2"></i>
                            <strong>@trophy.Name</strong>
                        </div>
                        <AuthorizeView Roles="Admin,Owner">
                            <Authorized>
                                <div class="btn-group btn-group-sm">
                                    <button class="btn btn-outline-primary" @onclick="() => OpenEditTrophyModal(trophy)" title="Editar">
                                        <i class="bi bi-pencil"></i>
                                    </button>
                                    <button class="btn btn-outline-danger" @onclick="() => OpenDeleteTrophyModal(trophy)" title="Eliminar">
                                        <i class="bi bi-trash"></i>
                                    </button>
                                </div>
                            </Authorized>
                        </AuthorizeView>
                    </div>
                }
            </div>
        }
    </BodyContent>
    <FooterContent>
        <button type="button" class="btn btn-secondary" @onclick="CloseTrophyModal">Fechar</button>
    </FooterContent>
</Modal>

<!-- Trophy Edit Modal -->
<Modal Show="@showTrophyEditModal"
       ShowChanged="@((bool show) => showTrophyEditModal = show)"
       Title="@(isTrophyCreateMode ? "Adicionar Troféu" : "Editar Troféu")"
       Size="Modal.ModalSize.Default"
       Centered="true">
    <BodyContent>
        @if (editingTrophy != null)
        {
            <EditForm Model="editingTrophy" OnValidSubmit="SaveTrophy">
                <DataAnnotationsValidator />
                <ValidationSummary class="text-danger" />

                <div class="mb-3">
                    <label class="form-label">Nome do Troféu</label>
                    <InputText class="form-control" @bind-Value="editingTrophy.Name" placeholder="Ex: 1º Lugar, Melhor Apresentação..." />
                </div>

                <div class="d-flex justify-content-end gap-2">
                    <button type="button" class="btn btn-secondary" @onclick="CloseTrophyEditModal">Cancelar</button>
                    <button type="submit" class="btn btn-primary btn-primary-purple">Guardar</button>
                </div>
            </EditForm>
        }
    </BodyContent>
</Modal>

<!-- Trophy Delete Modal -->
<Modal Show="@showTrophyDeleteModal"
       ShowChanged="@((bool show) => showTrophyDeleteModal = show)"
       Title="Confirmar Eliminação"
       Size="Modal.ModalSize.Default"
       Centered="true">
    <BodyContent>
        <p>Tem a certeza que deseja eliminar o troféu <strong>@deletingTrophy?.Name</strong>?</p>
        <p class="text-muted small">Esta ação não pode ser revertida.</p>
    </BodyContent>
    <FooterContent>
        <button type="button" class="btn btn-secondary" @onclick="CloseTrophyDeleteModal">Cancelar</button>
        <button type="button" class="btn btn-danger" @onclick="DeleteTrophy">Eliminar</button>
    </FooterContent>
</Modal>

<!-- Delete Enrollment Confirmation Modal (Owner Only) -->
<Modal Show="@showDeleteEnrollmentModal"
       ShowChanged="@((bool show) => showDeleteEnrollmentModal = show)"
       Title="Confirmar Eliminação de Inscrição"
       Size="Modal.ModalSize.Default"
       Centered="true">
    <BodyContent>
        @if (deletingEnrollment != null)
        {
            <p>Tem a certeza que deseja eliminar a inscrição de <strong>@deletingEnrollment.User?.GetDisplayName()</strong> neste evento?</p>
            <p class="text-muted small">Esta ação não pode ser revertida.</p>
        }
    </BodyContent>
    <FooterContent>
        <button type="button" class="btn btn-secondary" @onclick="CloseDeleteEnrollmentModal">Cancelar</button>
        <button type="button" class="btn btn-danger" @onclick="DeleteEnrollment">Eliminar</button>
    </FooterContent>
</Modal>

@code {
    private List<Event>? events;
    private List<Enrollment>? enrollments;
    private List<Enrollment>? eventEnrollments;
    private ApplicationUser? currentUser;
    private bool showEditModal = false;
    private bool showDeleteModal = false;
    private bool showEnrollModal = false;
    private bool showRemoveEnrollmentModal = false;
    private bool showEnrollmentListModal = false;
    private bool showCropperModal = false;
    private bool showRepertoireModal = false;
    private bool showEventDetailsModal = false;
    private bool showTrophyModal = false;
    private bool showTrophyEditModal = false;
    private bool showTrophyDeleteModal = false;
    private bool showDeleteEnrollmentModal = false;
    private bool isCreateMode = false;
    private bool isEditingEnrollment = false;
    private bool wantToPlay = false; // Toggle for "Quero tocar"
    private bool isUserAdmin = false;
    private bool isUserOwner = false;
    private bool isTrophyCreateMode = false;
    private Event? editingEvent;
    private Event? deletingEvent;
    private Event? selectedEvent;
    private Event? selectedEventForRepertoire;
    private Event? selectedEventForTrophies;
    private Enrollment? enrollmentForm;
    private Enrollment? removingEnrollment;
    private Enrollment? deletingEnrollment;
    private Trophy? editingTrophy;
    private Trophy? deletingTrophy;
    private List<Trophy>? eventTrophies;
    private string? uploadedImagePath;
    private string dateValidationError = string.Empty;
    private ImageCropper? imageCropper;
    private IBrowserFile? selectedFile;
    private long eventImageVersion = DateTime.UtcNow.Ticks;
    private ImageUploadManager? imageUploadManager;

    // Previous events (admin only)
    private List<Event>? allPreviousEvents;
    private List<Event>? filteredPreviousEvents;
    private string previousEventsSearch = string.Empty;
    
    // Helper instances for previous events
    private SearchHelper<Event> searchHelper = new();
    private PaginationHelper<Event> paginationHelper = new() { PageSize = 9 };
    private List<Event> paginatedPreviousEvents => paginationHelper.GetPageData(filteredPreviousEvents ?? new List<Event>());
    
    // Enrollment modal filter variables
    private string enrollmentSearchTerm = string.Empty;
    private SortableTableHelper<Enrollment> enrollmentSortHelper = new() { SortColumn = "EnrolledAt", SortAscending = false };
    private List<Enrollment> filteredPerformingMembers = new();
    private List<Enrollment> filteredLeitoes = new();
    
    // Pagination for enrollment modals
    private PaginationHelper<Enrollment> performingMembersPagination = new() { PageSize = 8 };
    private PaginationHelper<Enrollment> leitoesPagination = new() { PageSize = 8 };
    private List<Enrollment> paginatedPerformingMembers => performingMembersPagination.GetPageData(filteredPerformingMembers ?? new List<Enrollment>());
    private List<Enrollment> paginatedLeitoes => leitoesPagination.GetPageData(filteredLeitoes ?? new List<Enrollment>());

    protected override async Task OnInitializedAsync()
    {
        await LoadEvents();
        await LoadPreviousEvents();
        await LoadEnrollments();
        await LoadCurrentUser();
    }

    private async Task LoadEvents()
    {
        events = (await EventService.GetUpcomingEventsAsync(100)).ToList();
        eventImageVersion = DateTime.UtcNow.Ticks; // Refresh cache-bust parameter
    }

    private async Task LoadEnrollments()
    {
        enrollments = (await EnrollmentService.GetAllEnrollmentsAsync()).ToList();
    }

    private async Task LoadPreviousEvents()
    {
        allPreviousEvents = (await EventService.GetPastEventsAsync(100)).ToList();
        FilterPreviousEvents();
    }

    private void FilterPreviousEvents()
    {
        if (allPreviousEvents == null)
        {
            filteredPreviousEvents = new List<Event>();
            return;
        }

        // Use SearchHelper for filtering
        searchHelper.SearchTerm = previousEventsSearch;
        filteredPreviousEvents = searchHelper.FilterMultiple(allPreviousEvents, new List<Func<Event, string>>
        {
            e => e.Name ?? "",
            e => e.Location ?? "",
            e => e.Description ?? ""
        });

        paginationHelper.Reset(); // Reset to first page when filtering
    }

    private async Task HandlePreviousEventsSearch(string searchTerm)
    {
        previousEventsSearch = searchTerm;
        FilterPreviousEvents();
        await Task.CompletedTask;
    }

    private async Task HandlePreviousEventsPageChange(int newPage)
    {
        paginationHelper.GoToPage(newPage);
        await Task.CompletedTask;
    }

    private async Task HandlePreviousEventsPageSizeChange(int newPageSize)
    {
        paginationHelper.PageSize = newPageSize;
        paginationHelper.Reset();
        await Task.CompletedTask;
    }

    private void ChangePreviousEventsPage(int newPage)
    {
        paginationHelper.GoToPage(newPage);
    }

    private async Task LoadCurrentUser()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var userName = authState.User.Identity?.Name;

        if (!string.IsNullOrEmpty(userName))
        {
            currentUser = await UserManager.FindByNameAsync(userName);
            isUserAdmin = authState.User.IsInRole("Admin") || authState.User.IsInRole("Owner");
            isUserOwner = authState.User.IsInRole("Owner");
        }
    }

    private Enrollment? GetUserEnrollment(int eventId)
    {
        if (currentUser == null || enrollments == null) return null;
        return enrollments.FirstOrDefault(e => e.EventId == eventId && e.UserId == currentUser.Id);
    }

    private int GetEnrollmentCount(int eventId)
    {
        if (enrollments == null) return 0;
        return enrollments.Count(e => e.EventId == eventId);
    }

    private void OpenCreateModal()
    {
        isCreateMode = true;
        uploadedImagePath = null;
        dateValidationError = string.Empty;
        editingEvent = new Event
        {
            Date = DateTime.Today.AddDays(1),
            Name = string.Empty,
            Description = string.Empty,
            ImageUrl = string.Empty,
            Location = string.Empty,
            Type = EventType.Atuacao
        };
        showEditModal = true;
    }

    private void OpenEditModal(Event eventItem)
    {
        isCreateMode = false;
        uploadedImagePath = null;
        dateValidationError = string.Empty;
        editingEvent = new Event
        {
            Id = eventItem.Id,
            Name = eventItem.Name,
            Date = eventItem.Date,
            EndDate = eventItem.EndDate,
            Description = eventItem.Description,
            ImageData = eventItem.ImageData,
            ImageContentType = eventItem.ImageContentType,
            ImageUrl = eventItem.ImageUrl,
            Location = eventItem.Location,
            Type = eventItem.Type,
            CreatedAt = eventItem.CreatedAt
        };
        showEditModal = true;
    }

    private void CloseEditModal()
    {
        showEditModal = false;
        editingEvent = null;
        uploadedImagePath = null;
    }

    private async Task HandleImageUpload(IBrowserFile file)
    {
        try
        {
            if (file != null)
            {
                var maxFileSize = 10 * 1024 * 1024;
                if (file.Size > maxFileSize)
                {
                    return;
                }

                selectedFile = file;

                if (imageCropper != null)
                {
                    await imageCropper.LoadImageAsync(selectedFile);
                }
            }
        }
        catch (Exception)
        {
            // Handle error silently or show message
        }
    }

    private void OnImageCropped(byte[] croppedImageData)
    {
        try
        {
            if (editingEvent != null && croppedImageData != null && croppedImageData.Length > 0)
            {
                editingEvent.ImageData = croppedImageData;
                editingEvent.ImageContentType = "image/jpeg";
                uploadedImagePath = "uploaded";
                
                // Update cache-bust parameter to force image reload after save
                eventImageVersion = DateTime.UtcNow.Ticks;
                
                StateHasChanged();
            }
        }
        catch (Exception)
        {
            // Handle error
        }
    }

    private void OnCropperModalChanged(bool isOpen)
    {
        showCropperModal = isOpen;
        StateHasChanged();
    }

    private async Task SaveEvent()
    {
        if (editingEvent == null) return;

        // Validate date is in the future only for new events
        dateValidationError = string.Empty;
        if (isCreateMode && editingEvent.Date.Date <= DateTime.Today)
        {
            dateValidationError = "Event date must be after today.";
            return;
        }

        if (isCreateMode)
        {
            var newEvent = await EventService.CreateEventAsync(
                editingEvent.Name,
                editingEvent.Date,
                editingEvent.Location,
                editingEvent.Type,
                editingEvent.Description ?? string.Empty
            );

            // Update with EndDate if provided
            await EventService.UpdateEventAsync(
                newEvent.Id,
                newEvent.Name,
                newEvent.Date,
                newEvent.Location,
                newEvent.Description ?? string.Empty,
                editingEvent.EndDate
            );

            if (editingEvent.ImageData != null)
            {
                await EventService.SetEventImageAsync(
                    newEvent.Id,
                    editingEvent.ImageData,
                    editingEvent.ImageContentType
                );
            }
        }
        else
        {
            await EventService.UpdateEventAsync(
                editingEvent.Id,
                editingEvent.Name,
                editingEvent.Date,
                editingEvent.Location,
                editingEvent.Description ?? string.Empty,
                editingEvent.EndDate
            );

            // Update image data if new image was uploaded
            if (editingEvent.ImageData != null)
            {
                await EventService.SetEventImageAsync(
                    editingEvent.Id,
                    editingEvent.ImageData,
                    editingEvent.ImageContentType
                );
            }
        }

        await LoadEvents();
        await LoadPreviousEvents();
        CloseEditModal();
    }

    private void OpenDeleteModal(Event eventItem)
    {
        deletingEvent = eventItem;
        showDeleteModal = true;
    }

    private void CloseDeleteModal()
    {
        showDeleteModal = false;
        deletingEvent = null;
    }

    private async Task DeleteEvent()
    {
        if (deletingEvent == null) return;

        await EventService.DeleteEventAsync(deletingEvent.Id);
        await LoadEvents();
        await LoadPreviousEvents();

        CloseDeleteModal();
    }

    // Enrollment methods
    private void OpenEnrollModal(Event eventItem)
    {
        if (currentUser == null) return;

        isEditingEnrollment = false;
        selectedEvent = eventItem;
        wantToPlay = true; // Default to showing instrument field
        enrollmentForm = new Enrollment
        {
            UserId = currentUser.Id,
            EventId = eventItem.Id,
            Instrument = null,
            Notes = string.Empty
        };
        showEnrollModal = true;
    }

    private void OpenEditEnrollmentModal(Enrollment enrollment)
    {
        isEditingEnrollment = true;
        selectedEvent = events?.FirstOrDefault(e => e.Id == enrollment.EventId);
        wantToPlay = enrollment.Instrument.HasValue; // Set toggle based on whether instrument is set
        enrollmentForm = new Enrollment
        {
            Id = enrollment.Id,
            UserId = enrollment.UserId,
            EventId = enrollment.EventId,
            Instrument = enrollment.Instrument,
            Notes = enrollment.Notes
        };
        showEnrollModal = true;
    }

    private void OnWantToPlayChanged(bool value)
    {
        wantToPlay = value;
        if (!value && enrollmentForm != null)
        {
            // Clear instrument when toggle is turned off
            enrollmentForm.Instrument = null;
        }
    }

    private void CloseEnrollModal()
    {
        showEnrollModal = false;
        enrollmentForm = null;
        selectedEvent = null;
        isEditingEnrollment = false;
        wantToPlay = false;
    }

    private async Task SaveEnrollment()
    {
        if (enrollmentForm == null || currentUser == null) return;

        if (isEditingEnrollment)
        {
            var existingEnrollment = await EnrollmentService.GetEnrollmentByIdAsync(enrollmentForm.Id);
            if (existingEnrollment != null && existingEnrollment.UserId == currentUser.Id)
            {
                // Update enrollment - delete old and create new with updated data
                await EnrollmentService.DeleteEnrollmentAsync(enrollmentForm.Id);
                await EnrollmentService.CreateEnrollmentAsync(
                    enrollmentForm.UserId,
                    enrollmentForm.EventId,
                    enrollmentForm.Instrument,
                    enrollmentForm.Notes
                );
            }
        }
        else
        {
            await EnrollmentService.CreateEnrollmentAsync(
                enrollmentForm.UserId,
                enrollmentForm.EventId,
                enrollmentForm.Instrument,
                enrollmentForm.Notes
            );
        }

        await LoadEnrollments();
        CloseEnrollModal();
        StateHasChanged();
    }

    private void OpenRemoveEnrollmentModal(Enrollment enrollment)
    {
        removingEnrollment = enrollment;
        showRemoveEnrollmentModal = true;
    }

    private void CloseRemoveEnrollmentModal()
    {
        showRemoveEnrollmentModal = false;
        removingEnrollment = null;
    }

    private async Task RemoveEnrollment()
    {
        if (removingEnrollment == null || currentUser == null) return;

        var enrollmentToRemove = await EnrollmentService.GetEnrollmentByIdAsync(removingEnrollment.Id);
        if (enrollmentToRemove != null && enrollmentToRemove.UserId == currentUser.Id)
        {
            await EnrollmentService.DeleteEnrollmentAsync(removingEnrollment.Id);
            await LoadEnrollments();
        }

        CloseRemoveEnrollmentModal();
        StateHasChanged();
    }

    private async Task OpenEnrollmentListModal(Event eventItem)
    {
        selectedEvent = eventItem;
        eventEnrollments = (await EnrollmentService.GetEnrollmentsByEventIdAsync(eventItem.Id)).ToList();
        enrollmentSearchTerm = string.Empty;
        enrollmentSortHelper = new() { SortColumn = "EnrolledAt", SortAscending = false };
        FilterEnrollments();
        showEnrollmentListModal = true;
    }

    private void CloseEnrollmentListModal()
    {
        showEnrollmentListModal = false;
        selectedEvent = null;
        eventEnrollments = null;
        filteredPerformingMembers.Clear();
        filteredLeitoes.Clear();
    }
    
    private void FilterEnrollments()
    {
        if (eventEnrollments == null)
        {
            filteredPerformingMembers = new List<Enrollment>();
            filteredLeitoes = new List<Enrollment>();
            return;
        }
        
        var performingMembers = eventEnrollments
            .Where(e => e.User != null && !e.User.Categories.Contains(MemberCategory.Leitao))
            .ToList();
        
        var leitaoMembers = eventEnrollments
            .Where(e => e.User != null && e.User.Categories.Contains(MemberCategory.Leitao))
            .ToList();
        
        // Apply search filter to performing members
        if (!string.IsNullOrWhiteSpace(enrollmentSearchTerm))
        {
            var searchTerms = enrollmentSearchTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.ToLower())
                .ToArray();
            
            performingMembers = performingMembers.Where(e =>
            {
                if (e.User == null) return false;
                
                var searchableText = $"{e.User.FirstName} {e.User.LastName} {e.User.Nickname}".ToLower();
                return searchTerms.All(term => searchableText.Contains(term));
            }).ToList();
        }
        
        filteredPerformingMembers = performingMembers;
        filteredLeitoes = leitaoMembers.OrderBy(e => e.User?.FirstName).ToList();
        
        // Reset to page 1 when search changes
        performingMembersPagination.CurrentPage = 1;
        leitoesPagination.CurrentPage = 1;
        
        ApplySortToEnrollments();
    }
    
    private void HandlePerformingMembersPageChange(int page)
    {
        performingMembersPagination.CurrentPage = page;
        StateHasChanged();
    }

    private void HandlePerformingMembersPageSizeChange(int pageSize)
    {
        performingMembersPagination.PageSize = pageSize;
        performingMembersPagination.CurrentPage = 1;
        StateHasChanged();
    }

    private void HandleLeitoesPageChange(int page)
    {
        leitoesPagination.CurrentPage = page;
        StateHasChanged();
    }

    private void HandleLeitoesPageSizeChange(int pageSize)
    {
        leitoesPagination.PageSize = pageSize;
        leitoesPagination.CurrentPage = 1;
        StateHasChanged();
    }
    
    private void SortEnrollmentsBy(string column)
    {
        enrollmentSortHelper.ChangeSortColumn(column);
        ApplySortToEnrollments();
    }
    
    private void ApplySortToEnrollments()
    {
        if (filteredPerformingMembers == null || !filteredPerformingMembers.Any())
            return;
        
        var columnSelectors = new Dictionary<string, Func<Enrollment, IComparable>>
        {
            [nameof(ApplicationUser.FirstName)] = e => e.User?.FirstName ?? "",
            ["Category"] = e =>
            {
                if (e.User == null) return "3";
                if (e.User.Positions.Contains(Position.Magister)) return "0";
                if (e.User.Categories.Contains(MemberCategory.Tuno)) return "1";
                if (e.User.Categories.Contains(MemberCategory.Caloiro)) return "2";
                return "3";
            },
            ["Instrument"] = e => e.Instrument?.ToString() ?? "ZZZ",
            ["EnrolledAt"] = e => e.EnrolledAt
        };
        
        filteredPerformingMembers = enrollmentSortHelper.ApplySort(filteredPerformingMembers, columnSelectors);
    }

    private string GetEventTypeDisplay(EventType type)
    {
        return type switch
        {
            EventType.Atuacao => "Atuação",
            _ => type.ToString()
        };
    }

    // Repertoire modal methods
    private void OpenRepertoireModal(Event eventItem)
    {
        selectedEventForRepertoire = eventItem;
        showRepertoireModal = true;
    }

    private void OpenEventDetailsModal(Event eventItem)
    {
        selectedEvent = eventItem;
        showEventDetailsModal = true;
    }

    private async Task HandleRepertoireChanged()
    {
        // Refresh data if needed
        await Task.CompletedTask;
    }

    // Trophy modal methods
    private async Task OpenTrophyModal(Event eventItem)
    {
        selectedEventForTrophies = eventItem;
        eventTrophies = (await TrophyService.GetByEventIdAsync(eventItem.Id)).ToList();
        showTrophyModal = true;
    }

    private void CloseTrophyModal()
    {
        showTrophyModal = false;
        selectedEventForTrophies = null;
        eventTrophies = null;
    }

    private void OpenCreateTrophyModal()
    {
        if (selectedEventForTrophies == null) return;
        
        editingTrophy = new Trophy(selectedEventForTrophies.Id);
        isTrophyCreateMode = true;
        showTrophyEditModal = true;
    }

    private void OpenEditTrophyModal(Trophy trophy)
    {
        editingTrophy = trophy;
        isTrophyCreateMode = false;
        showTrophyEditModal = true;
    }

    private void CloseTrophyEditModal()
    {
        showTrophyEditModal = false;
        editingTrophy = null;
        isTrophyCreateMode = false;
    }

    private async Task SaveTrophy()
    {
        if (editingTrophy == null) return;

        try
        {
            if (isTrophyCreateMode)
            {
                await TrophyService.CreateAsync(editingTrophy);
            }
            else
            {
                await TrophyService.UpdateAsync(editingTrophy);
            }

            // Refresh trophy list
            if (selectedEventForTrophies != null)
            {
                eventTrophies = (await TrophyService.GetByEventIdAsync(selectedEventForTrophies.Id)).ToList();
            }

            CloseTrophyEditModal();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving trophy: {ex.Message}");
        }
    }

    private void OpenDeleteTrophyModal(Trophy trophy)
    {
        deletingTrophy = trophy;
        showTrophyDeleteModal = true;
    }

    private void CloseTrophyDeleteModal()
    {
        showTrophyDeleteModal = false;
        deletingTrophy = null;
    }

    private async Task DeleteTrophy()
    {
        if (deletingTrophy == null) return;

        try
        {
            await TrophyService.DeleteAsync(deletingTrophy.Id);

            // Refresh trophy list
            if (selectedEventForTrophies != null)
            {
                eventTrophies = (await TrophyService.GetByEventIdAsync(selectedEventForTrophies.Id)).ToList();
            }

            CloseTrophyDeleteModal();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting trophy: {ex.Message}");
        }
    }
    
    // Owner-only enrollment deletion methods
    private void OpenDeleteEnrollmentModal(Enrollment enrollment)
    {
        deletingEnrollment = enrollment;
        showDeleteEnrollmentModal = true;
    }
    
    private void CloseDeleteEnrollmentModal()
    {
        showDeleteEnrollmentModal = false;
        deletingEnrollment = null;
    }
    
    private async Task DeleteEnrollment()
    {
        if (deletingEnrollment == null) return;
        
        try
        {
            await EnrollmentService.DeleteEnrollmentAsync(deletingEnrollment.Id);
            
            // Refresh the enrollment list for the selected event
            if (selectedEvent != null)
            {
                eventEnrollments = (await EnrollmentService.GetEnrollmentsByEventIdAsync(selectedEvent.Id)).ToList();
                enrollmentSearchTerm = string.Empty;
                FilterEnrollments();
            }
            
            // Also reload general enrollments to update counts
            await LoadEnrollments();
            
            CloseDeleteEnrollmentModal();
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting enrollment: {ex.Message}");
        }
    }
    
    private string GetEventImageUrl(string imageSrc)
    {
        // Add cache-busting parameter to force reload after image update
        if (!string.IsNullOrEmpty(imageSrc) && imageSrc.StartsWith("/api/images/"))
        {
            return $"{imageSrc}?v={eventImageVersion}";
        }
        return imageSrc ?? string.Empty;
    }
}

